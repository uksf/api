using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerMonitorTests : GameServerProcessManagerTestBase
{
    [Fact]
    public async Task Monitor_WhenProcessGone_ClearsStateAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClients = [],
            Status = new GameServerStatus { Running = true }
        };
        _servers.Add(server);

        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((Process)null);
        var callCount = 0;
        _mockContext.Setup(x => x.Get())
                    .Returns(() =>
                        {
                            callCount++;
                            return callCount == 1 ? new List<DomainGameServer> { server } : new List<DomainGameServer>();
                        }
                    );
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        _sut.EnsureMonitorRunning();
        await Task.Delay(1000);

        server.ProcessId.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.Server.Id == "s1")), Times.Once);
    }

    [Fact]
    public async Task Monitor_WhenProcessGone_KillsHeadlessClientsAndFinalisesSession()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClients = [new(5001, HeadlessClientStart), new(5002, HeadlessClientStart)],
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "sess-1" }
        };
        _servers.Add(server);

        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        var callCount = 0;
        _mockContext.Setup(x => x.Get())
                    .Returns(() =>
                        {
                            callCount++;
                            return callCount == 1 ? new List<DomainGameServer> { server } : new List<DomainGameServer>();
                        }
                    );
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        _sut.EnsureMonitorRunning();
        await Task.Delay(1000);

        _mockProcessUtilities.Verify(x => x.FindProcessById(5001), Times.Once);
        _mockProcessUtilities.Verify(x => x.FindProcessById(5002), Times.Once);
        server.HeadlessClients.Should().BeEmpty();
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("sess-1"), Times.Once);
        _mockOpSessionCaptureService.Verify(x => x.CaptureEndedAsync("sess-1"), Times.Once);
        _mockContext.Verify(x => x.Replace(server), Times.Once);
    }

    [Fact]
    public async Task Monitor_WhenNoServersAndNoOrphanedProcesses_ExitsAndPushesZeroCount()
    {
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        _sut.EnsureMonitorRunning();
        await Task.Delay(500);

        _mockServersClient.Verify(x => x.ReceiveInstanceCount(0), Times.Once);
    }

    [Fact]
    public async Task Monitor_WhenOrphanedProcesses_PushesInstanceCountWhenTheyDie()
    {
        var instanceCount = 2;
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses())
                    .Returns(() => instanceCount > 0 ? Enumerable.Range(0, instanceCount).Select(i => new ProcessCommandLineInfo(i + 1, "")).ToList() : []);

        _sut.EnsureMonitorRunning();
        await Task.Delay(500);

        instanceCount = 0;
        await Task.Delay(3000);

        _mockServersClient.Verify(x => x.ReceiveInstanceCount(0), Times.Once);
    }

    [Fact]
    public async Task Monitor_WhenOrphanedProcessesLingerBriefly_DoesNotForceKillBeforeCeiling()
    {
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, "")]);

        _sut.EnsureMonitorRunning();
        await Task.Delay(2500);

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Monitor_WhenServerLaunchedDuringOrphanDrain_ReconcilesItAfterDrainInsteadOfExiting()
    {
        var serverB = new DomainGameServer
        {
            Id = "b",
            Name = "B",
            ProcessId = 9999,
            HeadlessClients = [],
            Status = new GameServerStatus { Running = true }
        };
        _servers.Add(serverB);
        var getCallCount = 0;
        _mockContext.Setup(x => x.Get())
                    .Returns(() =>
                        {
                            getCallCount++;
                            return getCallCount == 1 ? new List<DomainGameServer>() : new List<DomainGameServer> { serverB };
                        }
                    );

        var orphanPresent = true;
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns(() => orphanPresent ? [new ProcessCommandLineInfo(1, "")] : []);
        _mockProcessUtilities.Setup(x => x.FindProcessById(9999)).Returns((Process)null);

        _sut.EnsureMonitorRunning();
        await Task.Delay(500);
        orphanPresent = false;

        await Task.Delay(3000);

        serverB.ProcessId.Should().BeNull();
    }
}
