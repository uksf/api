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

public class GameServerProcessManagerServerStatusTests : GameServerProcessManagerTestBase
{
    [Fact]
    public async Task HandleServerStatusAsync_UpdatesStatusAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            ProcessId = 1,
            Status = new GameServerStatus()
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetMaxPlayerCountFromConfig(server)).Returns("40");
        _mockHelpers.Setup(x => x.StripMilliseconds(It.IsAny<TimeSpan>())).Returns(TimeSpan.Zero);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, "")]);

        var data = new Dictionary<string, object>
        {
            { "map", "Altis" },
            { "mission", "test_mission" },
            { "players", new List<object> { "Alpha", "Bravo" } },
            { "uptime", "120.5" },
            { "entityCount", "10" },
            { "aiCount", "5" },
            { "headlessClientCount", "1" }
        };

        await _sut.HandleServerStatusAsync(2303, data);

        server.Status.Running.Should().BeTrue();
        server.Status.Launching.Should().BeFalse();
        server.Status.Map.Should().Be("Altis");
        server.Status.Mission.Should().Be("test_mission");
        server.Status.Players.Should().BeEquivalentTo("Alpha", "Bravo");
        server.Status.Uptime.Should().Be(120.5f);
        server.Status.EntityCount.Should().Be(10);
        server.Status.AiCount.Should().Be(5);
        server.Status.HeadlessClientCount.Should().Be(1);
        server.Status.MaxPlayers.Should().Be("40");
        server.Status.StartedAt.Should().NotBeNull();
        _mockContext.Verify(x => x.Replace(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
    }

    [Fact]
    public async Task HandleServerStatusAsync_WhenNoMatchingServer_LogsWarning()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns((DomainGameServer)null);

        await _sut.HandleServerStatusAsync(9999, new Dictionary<string, object>());

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("9999"))), Times.Once);
    }

    [Fact]
    public async Task HandleServerStatusAsync_AfterTheServerWasReset_DoesNotResurrectRunning()
    {
        var server = new DomainGameServer
        {
            Id = "s-reset",
            ApiPort = 2398,
            Status = new GameServerStatus()
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);

        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        await _sut.HandleServerStatusAsync(2398, new Dictionary<string, object> { { "map", "Altis" }, { "players", new List<object>() } });

        server.Status.Running.Should().BeFalse();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
    }

    [Fact]
    public async Task HandleServerStatusAsync_FromAnOlderProcess_IsIgnored()
    {
        var started = new DateTime(2026, 10, 14, 20, 0, 0, DateTimeKind.Utc);
        var server = new DomainGameServer
        {
            Id = "s-old",
            ApiPort = 2397,
            ProcessId = 1,
            ProcessStartedAt = started,
            Status = new GameServerStatus { Launching = true }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);

        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        await _sut.HandleServerStatusAsync(
            2397,
            new Dictionary<string, object> { { "map", "Altis" }, { "players", new List<object>() } },
            started.AddMinutes(-1)
        );

        server.Status.Running.Should().BeFalse();
        server.Status.Launching.Should().BeTrue();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
    }

    [Fact]
    public async Task HandleServerStatusAsync_WhenStopping_DoesNotResurrectRunning()
    {
        var server = new DomainGameServer
        {
            Id = "s-stopping-guard",
            ApiPort = 2399,
            Status = new GameServerStatus { StopPhase = StopPhase.Ending, StopPhaseEnteredAt = DateTime.UtcNow }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleServerStatusAsync(2399, new Dictionary<string, object> { { "map", "Altis" } });

        server.Status.Running.Should().BeFalse();
        server.Status.StopPhase.Should().Be(StopPhase.Ending);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Never);
    }

    [Fact]
    public async Task HandleServerStatusAsync_ParsesUptimeAndEntityCounts()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            ProcessId = 1,
            Status = new GameServerStatus()
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetMaxPlayerCountFromConfig(server)).Returns("40");
        _mockHelpers.Setup(x => x.StripMilliseconds(It.IsAny<TimeSpan>())).Returns(TimeSpan.FromSeconds(120));
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, "")]);

        var data = new Dictionary<string, object>
        {
            { "map", "Altis" },
            { "mission", "test" },
            { "uptime", "120.5" },
            { "entityCount", "500" },
            { "aiCount", "50" },
            { "headlessClientCount", "2" }
        };

        await _sut.HandleServerStatusAsync(2303, data);

        server.Status.Uptime.Should().BeApproximately(120.5f, 0.01f);
        server.Status.EntityCount.Should().Be(500);
        server.Status.AiCount.Should().Be(50);
        server.Status.HeadlessClientCount.Should().Be(2);
        server.Status.MaxPlayers.Should().Be("40");
        server.Status.Launching.Should().BeFalse();
    }
}
