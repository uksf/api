using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessMonitorTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory = new();
    private readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    private readonly Mock<IHubContext<ServersHub, IServersClient>> _mockServersHub = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IGameServersContext> _mockContext = new();
    private readonly Mock<IGameServersService> _mockService = new();
    private readonly Mock<IServersClient> _mockServersClient;

    public GameServerProcessMonitorTests()
    {
        var mockClients = new Mock<IHubClients<IServersClient>>();
        _mockServersClient = new Mock<IServersClient>();
        mockClients.Setup(x => x.All).Returns(_mockServersClient.Object);
        _mockServersHub.Setup(x => x.Clients).Returns(mockClients.Object);

        var mockScope = new Mock<IServiceScope>();
        var mockProvider = new Mock<IServiceProvider>();
        mockProvider.Setup(x => x.GetService(typeof(IGameServersContext))).Returns(_mockContext.Object);
        mockProvider.Setup(x => x.GetService(typeof(IGameServersService))).Returns(_mockService.Object);
        mockScope.Setup(x => x.ServiceProvider).Returns(mockProvider.Object);
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
    }

    [Fact]
    public async Task Tick_WhenProcessGone_ShouldClearServerStateAndPushUpdate()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClientProcessIds = [],
            LaunchedBy = "user-1",
            Status = new GameServerStatus { Running = true, Launching = false }
        };

        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((Process)null);
        var callCount = 0;
        _mockContext.Setup(x => x.Get())
                    .Returns(() =>
                        {
                            callCount++;
                            return callCount == 1 ? new List<DomainGameServer> { server } : new List<DomainGameServer>();
                        }
                    );
        _mockService.Setup(x => x.GetGameInstanceCount()).Returns(0);

        var monitor = CreateMonitor();
        monitor.EnsureRunning();
        await Task.Delay(1000);

        server.ProcessId.Should().BeNull();
        server.LaunchedBy.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        server.Status.Launching.Should().BeFalse();
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.Server.Id == "s1")), Times.Once);
    }

    [Fact]
    public async Task Tick_WhenProcessGone_ShouldKillHeadlessClients()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClientProcessIds = [5001, 5002],
            Status = new GameServerStatus { Running = true }
        };

        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        var callCount = 0;
        _mockContext.Setup(x => x.Get())
                    .Returns(() =>
                        {
                            callCount++;
                            return callCount == 1 ? new List<DomainGameServer> { server } : new List<DomainGameServer>();
                        }
                    );
        _mockService.Setup(x => x.GetGameInstanceCount()).Returns(0);

        var monitor = CreateMonitor();
        monitor.EnsureRunning();
        await Task.Delay(1000);

        _mockProcessUtilities.Verify(x => x.FindProcessById(5001), Times.Once);
        _mockProcessUtilities.Verify(x => x.FindProcessById(5002), Times.Once);
        server.HeadlessClientProcessIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Tick_WhenStoppingForOver30s_ShouldForceKill()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Stopping = true, StoppingInitiatedAt = DateTime.UtcNow.AddSeconds(-31) }
        };

        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((Process)null);
        var callCount = 0;
        _mockContext.Setup(x => x.Get())
                    .Returns(() =>
                        {
                            callCount++;
                            return callCount == 1 ? new List<DomainGameServer> { server } : new List<DomainGameServer>();
                        }
                    );
        _mockService.Setup(x => x.GetGameInstanceCount()).Returns(0);

        var monitor = CreateMonitor();
        monitor.EnsureRunning();
        await Task.Delay(1000);

        server.Status.Stopping.Should().BeFalse();
        server.ProcessId.Should().BeNull();
        _mockLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Force-killing"))), Times.Once);
    }

    [Fact]
    public async Task Loop_WhenNoServersHaveProcessId_ShouldStopAndAllowRestart()
    {
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());

        var monitor = CreateMonitor();
        monitor.EnsureRunning();
        await Task.Delay(500);

        // Loop should have stopped — calling EnsureRunning again should not throw
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());
        monitor.EnsureRunning();
    }

    private GameServerProcessMonitor CreateMonitor()
    {
        return new GameServerProcessMonitor(_mockScopeFactory.Object, _mockProcessUtilities.Object, _mockServersHub.Object, _mockLogger.Object);
    }
}
