using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class GameServersControllerTests
{
    private readonly Mock<IGameServersContext> _mockGameServersContext = new();
    private readonly Mock<IVariablesContext> _mockVariablesContext = new();
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IMissionsService> _mockMissionsService = new();
    private readonly Mock<IHubContext<ServersHub, IServersClient>> _mockServersHub = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IGameServerHelpers> _mockGameServerHelpers = new();
    private readonly Mock<IRptLogService> _mockRptLogService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly Mock<IGameServerProcessMonitor> _mockProcessMonitor = new();
    private readonly Mock<IServersClient> _mockServersClient = new();
    private readonly GameServersController _sut;

    public GameServersControllerTests()
    {
        var mockClients = new Mock<IHubClients<IServersClient>>();
        mockClients.Setup(x => x.All).Returns(_mockServersClient.Object);
        _mockServersHub.Setup(x => x.Clients).Returns(mockClients.Object);

        _mockServersClient.Setup(x => x.ReceiveServersUpdate(It.IsAny<GameServersUpdate>())).Returns(Task.CompletedTask);
        _mockServersClient.Setup(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>())).Returns(Task.CompletedTask);

        _sut = new GameServersController(
            _mockGameServersContext.Object,
            _mockVariablesContext.Object,
            _mockGameServersService.Object,
            _mockMissionsService.Object,
            _mockServersHub.Object,
            _mockVariablesService.Object,
            _mockGameServerHelpers.Object,
            _mockRptLogService.Object,
            _mockLogger.Object,
            _mockHttpContextService.Object,
            _mockProcessMonitor.Object
        );

        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public void GetGameServers_ShouldReturnGameServersUpdate()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1", Name = "Test" } };
        _mockGameServersContext.Setup(x => x.Get()).Returns(servers);
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);
        _mockGameServersService.Setup(x => x.GetGameInstanceCount()).Returns(1);

        var result = _sut.GetGameServers();

        result.Servers.Should().HaveCount(1);
        result.InstanceCount.Should().Be(1);
        result.Missions.Should().NotBeNull();
    }

    [Fact]
    public async Task StopServer_ShouldSetStoppingAndPushServerUpdate()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Status = new GameServerStatus { Running = true }
        };
        _mockGameServersContext.Setup(x => x.GetSingle("s1")).Returns(gameServer);
        _mockGameServersContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { gameServer });
        _mockGameServersService.Setup(x => x.GetGameInstanceCount()).Returns(1);

        await _sut.StopServer("s1");

        gameServer.Status.Stopping.Should().BeTrue();
        gameServer.Status.StoppingInitiatedAt.Should().NotBeNull();
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.Server.Id == "s1")), Times.Once);
        _mockProcessMonitor.Verify(x => x.EnsureRunning(), Times.Once);
    }

    [Fact]
    public async Task KillServer_ShouldPushServerUpdateAndCallEnsureRunning()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Status = new GameServerStatus { Running = true },
            ProcessId = 1234
        };
        _mockGameServersContext.Setup(x => x.GetSingle("s1")).Returns(gameServer);
        _mockGameServersContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { gameServer });
        _mockGameServersService.Setup(x => x.GetGameInstanceCount()).Returns(1);

        await _sut.KillServer("s1");

        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
        _mockProcessMonitor.Verify(x => x.EnsureRunning(), Times.Once);
    }

    [Fact]
    public async Task AddServer_ShouldPushServersUpdate()
    {
        var gameServer = new DomainGameServer { Id = "s1", Name = "New Server" };
        _mockGameServersContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);

        await _sut.AddServer(gameServer);

        _mockServersClient.Verify(x => x.ReceiveServersUpdate(It.IsAny<GameServersUpdate>()), Times.Once);
    }

    [Fact]
    public async Task DeleteServer_ShouldPushServersUpdate()
    {
        var gameServer = new DomainGameServer { Id = "s1", Name = "Test" };
        _mockGameServersContext.Setup(x => x.GetSingle("s1")).Returns(gameServer);
        _mockGameServersContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);

        await _sut.DeleteGameServer("s1");

        _mockServersClient.Verify(x => x.ReceiveServersUpdate(It.IsAny<GameServersUpdate>()), Times.Once);
    }

    [Fact]
    public async Task EditGameServer_ShouldPushServerUpdate()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Environment = GameEnvironment.Release
        };
        _mockGameServersContext.Setup(x => x.GetSingle("s1")).Returns(gameServer);
        _mockGameServersContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { gameServer });
        _mockGameServersService.Setup(x => x.GetGameInstanceCount()).Returns(1);

        await _sut.EditGameServer(gameServer);

        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
    }
}
