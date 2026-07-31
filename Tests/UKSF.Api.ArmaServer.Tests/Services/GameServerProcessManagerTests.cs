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

public class GameServerProcessManagerTests : GameServerProcessManagerTestBase
{
    [Fact]
    public void GetInstanceCount_ReturnsCountOfArmaProcesses()
    {
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, ""), new ProcessCommandLineInfo(2, "")]);

        _sut.GetInstanceCount().Should().Be(2);
    }

    [Fact]
    public void GetInstanceCount_WhenNoProcesses_ReturnsZero()
    {
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        _sut.GetInstanceCount().Should().Be(0);
    }

    [Fact]
    public async Task PushServerUpdateAsync_SendsReceiveServerUpdateWithInstanceCount()
    {
        var server = new DomainGameServer { Id = "s1", Name = "Test" };
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, "")]);

        await _sut.PushServerUpdateAsync(server);

        _mockRptLogService.Verify(x => x.GetLogSources(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.Server.Id == "s1" && u.InstanceCount == 1)), Times.Once);
    }

    [Fact]
    public async Task PushServerUpdateAsync_SetsLogSourcesOnServer()
    {
        var server = new DomainGameServer { Id = "s1" };
        var logSources = new List<RptLogSource> { new("server", true) };
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        _mockRptLogService.Setup(x => x.GetLogSources(server)).Returns(logSources);

        await _sut.PushServerUpdateAsync(server);

        server.LogSources.Should().BeEquivalentTo(logSources);
    }

    [Fact]
    public async Task PushAllServersUpdateAsync_SendsReceiveServersUpdateWithAllData()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1" } };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.PushAllServersUpdateAsync();

        _mockServersClient.Verify(x => x.ReceiveServersUpdate(It.Is<GameServersUpdate>(u => u.Servers.Count == 1 && u.InstanceCount == 0)), Times.Once);
    }

    [Fact]
    public async Task PushAllServersUpdateAsync_SetsLogSourcesOnAllServers()
    {
        var server1 = new DomainGameServer { Id = "s1" };
        var server2 = new DomainGameServer { Id = "s2" };
        var servers = new List<DomainGameServer> { server1, server2 };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        _mockRptLogService.Setup(x => x.GetLogSources(It.IsAny<DomainGameServer>())).Returns(new List<RptLogSource> { new("server", true) });

        await _sut.PushAllServersUpdateAsync();

        _mockRptLogService.Verify(x => x.GetLogSources(server1), Times.Once);
        _mockRptLogService.Verify(x => x.GetLogSources(server2), Times.Once);
        server1.LogSources.Should().HaveCount(1);
        server2.LogSources.Should().HaveCount(1);
    }

    [Fact]
    public async Task PushAllServersUpdateAsync_IncludesMissionsInUpdate()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1" } };
        var tempFile = Path.Combine(Path.GetTempPath(), "mission1.Altis.pbo");
        File.WriteAllBytes(tempFile, []);
        var missions = new List<MissionFile> { new(new FileInfo(tempFile)) };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns(missions);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.PushAllServersUpdateAsync();

        _mockServersClient.Verify(x => x.ReceiveServersUpdate(It.Is<GameServersUpdate>(u => u.Missions.Count == 1)), Times.Once);
    }

    [Fact]
    public async Task KillServerAsync_ClearsStateAndPersistsAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            LaunchedBy = "user1",
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Running = true }
        };
        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((Process)null);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.KillServerAsync(server);

        server.ProcessId.Should().BeNull();
        server.LaunchedBy.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        _mockContext.Verify(x => x.Replace(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.Server.Id == "s1")), Times.Once);
    }

    [Fact]
    public async Task KillServerAsync_KillsHeadlessClients()
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
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.KillServerAsync(server);

        _mockProcessUtilities.Verify(x => x.FindProcessById(5001), Times.Once);
        _mockProcessUtilities.Verify(x => x.FindProcessById(5002), Times.Once);
        server.HeadlessClientProcessIds.Should().BeEmpty();
    }

    [Fact]
    public async Task KillServerAsync_FinalisesActiveSession()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = null,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "session-1" }
        };
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.KillServerAsync(server);

        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("session-1"), Times.Once);
        _mockOpSessionCaptureService.Verify(x => x.CaptureEndedAsync("session-1"), Times.Once);
    }

    [Fact]
    public async Task KillAllAsync_ClearsAllServersAndReturnsKillCount()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                ProcessId = 1234,
                HeadlessClientProcessIds = [],
                Status = new GameServerStatus { Running = true }
            },
            new()
            {
                Id = "s2",
                ProcessId = 5678,
                HeadlessClientProcessIds = [],
                Status = new GameServerStatus { Running = true }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        var killed = await _sut.KillAllAsync();

        killed.Should().Be(0);
        servers.Should()
               .AllSatisfy(s =>
                   {
                       s.ProcessId.Should().BeNull();
                       s.Status.Running.Should().BeFalse();
                   }
               );
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Exactly(2));
    }

    [Fact]
    public async Task KillAllAsync_WhenOneProcessThrowsOnKill_StillKillsOthersAndCleansUpAllServers()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                ProcessId = 1234,
                HeadlessClientProcessIds = [],
                Status = new GameServerStatus { Running = true }
            },
            new()
            {
                Id = "s2",
                ProcessId = 5678,
                HeadlessClientProcessIds = [],
                Status = new GameServerStatus { Running = true }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1234, ""), new ProcessCommandLineInfo(5678, "")]);
        // Same-PID handles to the live test process: HasExited never flips, and Kill is
        // fully intercepted via the mocked seam, so the real process is never touched.
        var process1 = Process.GetCurrentProcess();
        var process2 = Process.GetCurrentProcess();
        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns(process1);
        _mockProcessUtilities.Setup(x => x.FindProcessById(5678)).Returns(process2);
        _mockProcessUtilities.Setup(x => x.KillProcess(process1, It.IsAny<bool>())).Throws<Win32Exception>();

        var killed = await _sut.KillAllAsync();

        killed.Should().Be(2);
        _mockProcessUtilities.Verify(x => x.KillProcess(process2, It.IsAny<bool>()), Times.Once);
        servers.Should()
               .AllSatisfy(s =>
                   {
                       s.ProcessId.Should().BeNull();
                       s.Status.Running.Should().BeFalse();
                   }
               );
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Exactly(2));
    }

    [Fact]
    public void KillOrphanedArmaProcesses_WhenOneProcessThrowsOnKill_StillKillsTheOthers()
    {
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1234, ""), new ProcessCommandLineInfo(5678, "")]);
        var process1 = Process.GetCurrentProcess();
        var process2 = Process.GetCurrentProcess();
        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns(process1);
        _mockProcessUtilities.Setup(x => x.FindProcessById(5678)).Returns(process2);
        _mockProcessUtilities.Setup(x => x.KillProcess(process1, It.IsAny<bool>())).Throws<Win32Exception>();

        var act = () => _sut.KillOrphanedArmaProcesses();

        act.Should().NotThrow();
        _mockProcessUtilities.Verify(x => x.KillProcess(process2, It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task LaunchServerAsync_LaunchesProcessAndHCsAndPersists()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            NumberHeadlessClients = 1,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus()
        };
        _mockHelpers.Setup(x => x.GetGameServerExecutablePath(server)).Returns("arma3server_x64.exe");
        _mockHelpers.Setup(x => x.FormatGameServerLaunchArguments(server)).Returns("-port=2302");
        _mockHelpers.Setup(x => x.FormatHeadlessClientLaunchArguments(server, 0)).Returns("-port=2302 -client");
        _mockHelpers.Setup(x => x.GetGameServerConfigPath(server)).Returns(Path.Combine(Path.GetTempPath(), "test_config.cfg"));
        _mockHelpers.Setup(x => x.FormatGameServerConfig(server, 40, "mission.Altis.pbo")).Returns("config content");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302")).Returns(1234);
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302 -client")).Returns(5001);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, ""), new ProcessCommandLineInfo(2, "")]);

        await _sut.LaunchServerAsync(server, "mission.Altis.pbo", "user1", 40);

        server.ProcessId.Should().Be(1234);
        server.Status.Launching.Should().BeTrue();
        server.Status.Mission.Should().Be("mission");
        server.Status.Map.Should().Be("Altis");
        server.LaunchedBy.Should().Be("user1");
        server.HeadlessClientProcessIds.Should().Contain(5001);
        _mockContext.Verify(x => x.Replace(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
    }

    [Fact]
    public async Task LaunchServerAsync_ClearsStaleHeadlessClientProcessIdsBeforeRelaunch()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            NumberHeadlessClients = 1,
            HeadlessClientProcessIds = [9999], // stale from a previous run
            Status = new GameServerStatus()
        };
        _mockHelpers.Setup(x => x.GetGameServerExecutablePath(server)).Returns("arma3server_x64.exe");
        _mockHelpers.Setup(x => x.FormatGameServerLaunchArguments(server)).Returns("-port=2302");
        _mockHelpers.Setup(x => x.FormatHeadlessClientLaunchArguments(server, 0)).Returns("-port=2302 -client");
        _mockHelpers.Setup(x => x.GetGameServerConfigPath(server)).Returns(Path.Combine(Path.GetTempPath(), "test_config.cfg"));
        _mockHelpers.Setup(x => x.FormatGameServerConfig(server, 40, "mission.Altis.pbo")).Returns("config content");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302")).Returns(1234);
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302 -client")).Returns(5001);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.LaunchServerAsync(server, "mission.Altis.pbo", "user1", 40);

        server.HeadlessClientProcessIds.Should().BeEquivalentTo([5001]);
    }

    [Fact]
    public async Task LaunchServerAsync_WhenMissionNameHasNoPboExtension_StillParsesMap()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            NumberHeadlessClients = 0,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus()
        };
        _mockHelpers.Setup(x => x.GetGameServerExecutablePath(server)).Returns("arma3server_x64.exe");
        _mockHelpers.Setup(x => x.FormatGameServerLaunchArguments(server)).Returns("-port=2302");
        _mockHelpers.Setup(x => x.GetGameServerConfigPath(server)).Returns(Path.Combine(Path.GetTempPath(), "test_config.cfg"));
        _mockHelpers.Setup(x => x.FormatGameServerConfig(server, 40, "mission.Altis")).Returns("config content");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("arma3server_x64.exe", "-port=2302")).Returns(1234);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.LaunchServerAsync(server, "mission.Altis", "user1", 40);

        server.Status.Mission.Should().Be("mission");
        server.Status.Map.Should().Be("Altis");
    }

    [Fact]
    public async Task HandleServerStatusAsync_UpdatesStatusAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            Status = new GameServerStatus()
        };
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
    public async Task HandleServerStatusAsync_WhenStopping_DoesNotResurrectRunning()
    {
        var server = new DomainGameServer
        {
            Id = "s-stopping-guard",
            ApiPort = 2399,
            Status = new GameServerStatus { StopPhase = StopPhase.Ending, StopPhaseEnteredAt = DateTime.UtcNow }
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleServerStatusAsync(2399, new Dictionary<string, object> { { "map", "Altis" } });

        server.Status.Running.Should().BeFalse();
        server.Status.StopPhase.Should().Be(StopPhase.Ending);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Never);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenNoProcesses_ResetsAllServers()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                ProcessId = 1234,
                HeadlessClientProcessIds = [],
                Status = new GameServerStatus { Running = true }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        var result = await _sut.GetAllServerStatusesAsync();

        result[0].Status.Running.Should().BeFalse();
        result[0].ProcessId.Should().BeNull();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Once);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenSkipFeatureEnabled_ReturnsServersUnchangedWithoutWriting()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1", Status = new GameServerStatus { Running = true } } };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(true);

        var result = await _sut.GetAllServerStatusesAsync();

        result.Should().HaveCount(1);
        result[0].Status.Running.Should().BeTrue();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never); // no-op write removed (S3)
        _mockHelpers.Verify(x => x.GetGameServerArmaProcesses(), Times.Never);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenNoProcesses_FinalisesActiveSessions()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                HeadlessClientProcessIds = [],
                Status = new GameServerStatus { CurrentMissionSessionId = "session-1" }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.GetAllServerStatusesAsync();

        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("session-1"), Times.Once);
        _mockOpSessionCaptureService.Verify(x => x.CaptureEndedAsync("session-1"), Times.Once);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenNoProcesses_ClearsHeadlessClientProcessIds()
    {
        var servers = new List<DomainGameServer>
        {
            new()
            {
                Id = "s1",
                HeadlessClientProcessIds = [5001, 5002],
                Status = new GameServerStatus { Running = true }
            }
        };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.GetAllServerStatusesAsync();

        servers[0].HeadlessClientProcessIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenOneServerReconcileThrows_StillProcessesOthersAndDoesNotThrow()
    {
        var serverA = new DomainGameServer
        {
            Id = "a",
            Name = "A",
            ApiPort = 2303,
            Port = 2302,
            ProcessId = 1234,
            Status = new GameServerStatus { CurrentMissionSessionId = "sess-a" }
        };
        var serverB = new DomainGameServer
        {
            Id = "b",
            Name = "B",
            ApiPort = 2313,
            Port = 2312,
            ProcessId = 5678,
            Status = new GameServerStatus { CurrentMissionSessionId = "sess-b" }
        };
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { serverA, serverB });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        // Non-empty arma processes (skip the no-arma branch) that match neither server's main process,
        // so both servers route through the gone-branch -> HandleProcessGone -> Replace.
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1, "")]);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        _mockContext.Setup(x => x.Replace(serverA)).ThrowsAsync(new Exception("boom"));

        var act = async () => await _sut.GetAllServerStatusesAsync();

        await act.Should().NotThrowAsync();
        _mockContext.Verify(x => x.Replace(serverB), Times.Once); // sibling still reconciled
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("sess-b"), Times.Once);
        _mockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenNoArmaProcesses_OneResetThrows_StillResetsOthers()
    {
        var serverA = new DomainGameServer
        {
            Id = "a",
            Name = "A",
            ProcessId = 1,
            Status = new GameServerStatus { CurrentMissionSessionId = "sess-a" }
        };
        var serverB = new DomainGameServer
        {
            Id = "b",
            Name = "B",
            ProcessId = 2,
            Status = new GameServerStatus { CurrentMissionSessionId = "sess-b" }
        };
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { serverA, serverB });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]); // no-arma branch
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        _mockContext.Setup(x => x.Replace(serverA)).ThrowsAsync(new Exception("boom"));

        var act = async () => await _sut.GetAllServerStatusesAsync();

        await act.Should().NotThrowAsync();
        _mockContext.Verify(x => x.Replace(serverB), Times.Once); // sibling still reset despite serverA throwing
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("sess-b"), Times.Once);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WithProcesses_QueriesStatusEndpoint()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Port = 2302,
            ApiPort = 2303,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus()
        };
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { server });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(5678, "-config=ServerConfigs/Main.cfg -port=2302 ")]);

        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.RequestTimeout);
        var httpClient = new HttpClient(mockHandler);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _sut.GetAllServerStatusesAsync();

        result.Should().HaveCount(1);
        result[0].ProcessId.Should().Be(5678);
        mockHandler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_ScansArmaProcessesOnlyOncePerCall()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Port = 2302,
            ApiPort = 2303,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus()
        };
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { server });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(5678, "-config=ServerConfigs/Main.cfg -port=2302 ")]);
        var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.RequestTimeout));
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        await _sut.GetAllServerStatusesAsync();

        _mockHelpers.Verify(x => x.GetGameServerArmaProcesses(), Times.Once); // one OS process enumeration + WMI scan per poll, not two
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WithProcesses_NoMatchingProcess_ResetsServer()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Port = 2302,
            ApiPort = 2303,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Running = true }
        };
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer> { server });
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(5678, "-config=ServerConfigs/Other.cfg -port=2310 ")]);

        var result = await _sut.GetAllServerStatusesAsync();

        result[0].Status.Running.Should().BeFalse();
        result[0].ProcessId.Should().BeNull();
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenMainProcessGoneButHeadlessClientAlive_KillsAndClearsHeadlessClient()
    {
        var server = new DomainGameServer
        {
            Id = "leak-1",
            Name = "Test",
            ApiPort = 2303,
            Port = 2302,
            ProcessId = 1234,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "sess-1" }
        };
        var hcProcess = new ProcessCommandLineInfo(5001, "-port=2302 -client");
        _mockContext.Setup(x => x.Get()).Returns([server]);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([hcProcess]);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);

        await _sut.GetAllServerStatusesAsync();

        _mockProcessUtilities.Verify(x => x.FindProcessById(5001), Times.Once);
        server.HeadlessClientProcessIds.Should().BeEmpty();
        server.ProcessId.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("sess-1"), Times.Once);
    }

    [Fact]
    public async Task HandleServerStatusAsync_ParsesUptimeAndEntityCounts()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            Status = new GameServerStatus()
        };
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

    [Fact]
    public async Task Monitor_WhenProcessGone_ClearsStateAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Running = true }
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
            HeadlessClientProcessIds = [5001, 5002],
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "sess-1" }
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
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        _sut.EnsureMonitorRunning();
        await Task.Delay(1000);

        _mockProcessUtilities.Verify(x => x.FindProcessById(5001), Times.Once);
        _mockProcessUtilities.Verify(x => x.FindProcessById(5002), Times.Once);
        server.HeadlessClientProcessIds.Should().BeEmpty();
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

        // Should exit cleanly and allow restart
        _sut.EnsureMonitorRunning();
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

        _mockProcessUtilities.Verify(x => x.FindProcessById(1), Times.Never);
    }

    [Fact]
    public async Task Monitor_WhenServerLaunchedDuringOrphanDrain_ReconcilesItAfterDrainInsteadOfExiting()
    {
        var serverB = new DomainGameServer
        {
            Id = "b",
            Name = "B",
            ProcessId = 9999,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Running = true }
        };
        var getCallCount = 0;
        _mockContext.Setup(x => x.Get())
                    .Returns(() =>
                        {
                            getCallCount++;
                            // First read (before drain starts): no DB server holds a ProcessId.
                            // Any later read (only reachable if the monitor loops back after drain
                            // instead of exiting) picks up B, launched while orphans were draining.
                            return getCallCount == 1 ? new List<DomainGameServer>() : new List<DomainGameServer> { serverB };
                        }
                    );

        var orphanPresent = true;
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns(() => orphanPresent ? [new ProcessCommandLineInfo(1, "")] : []);
        _mockProcessUtilities.Setup(x => x.FindProcessById(9999)).Returns((Process)null);

        _sut.EnsureMonitorRunning();
        await Task.Delay(500);
        orphanPresent = false; // orphan exits -> drain completes

        await Task.Delay(3000);

        _mockProcessUtilities.Verify(x => x.FindProcessById(9999), Times.AtLeastOnce);
    }
}
