using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

public class GameServerProcessManagerTests
{
    private readonly Mock<IGameServersContext> _mockContext = new();
    private readonly Mock<IGameServerHelpers> _mockHelpers = new();
    private readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();
    private readonly Mock<IHubContext<ServersHub, IServersClient>> _mockServersHub = new();
    private readonly Mock<IMissionsService> _mockMissionsService = new();
    private readonly Mock<IRptLogService> _mockRptLogService = new();
    private readonly Mock<IMissionStatsService> _mockMissionStatsService = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IServersClient> _mockServersClient;
    private readonly GameServerProcessManager _sut;

    public GameServerProcessManagerTests()
    {
        var mockClients = new Mock<IHubClients<IServersClient>>();
        _mockServersClient = new Mock<IServersClient>();
        mockClients.Setup(x => x.All).Returns(_mockServersClient.Object);
        _mockServersHub.Setup(x => x.Clients).Returns(mockClients.Object);

        _sut = new GameServerProcessManager(
            _mockContext.Object,
            _mockHelpers.Object,
            _mockProcessUtilities.Object,
            _mockHttpClientFactory.Object,
            _mockServersHub.Object,
            _mockMissionsService.Object,
            _mockRptLogService.Object,
            _mockMissionStatsService.Object,
            _mockVariablesService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void GetInstanceCount_ReturnsCountOfArmaProcesses()
    {
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(new Process[2]);

        _sut.GetInstanceCount().Should().Be(2);
    }

    [Fact]
    public void GetInstanceCount_WhenNoProcesses_ReturnsZero()
    {
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        _sut.GetInstanceCount().Should().Be(0);
    }

    [Fact]
    public async Task PushServerUpdateAsync_SendsReceiveServerUpdateWithInstanceCount()
    {
        var server = new DomainGameServer { Id = "s1", Name = "Test" };
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(new Process[1]);

        await _sut.PushServerUpdateAsync(server);

        _mockRptLogService.Verify(x => x.GetLogSources(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.Server.Id == "s1" && u.InstanceCount == 1)), Times.Once);
    }

    [Fact]
    public async Task PushServerUpdateAsync_SetsLogSourcesOnServer()
    {
        var server = new DomainGameServer { Id = "s1" };
        var logSources = new List<RptLogSource> { new("server", true) };
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        await _sut.KillServerAsync(server);

        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("session-1"), Times.Once);
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

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
    public async Task StopServerAsync_SetsStoppingAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ApiPort = 2303,
            Status = new GameServerStatus { Running = true }
        };
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        await _sut.StopServerAsync(server);

        server.Status.Stopping.Should().BeTrue();
        server.Status.StoppingInitiatedAt.Should().NotBeNull();
        _mockContext.Verify(x => x.Replace(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
    }

    [Fact]
    public async Task StopServerAsync_WhenNotRunning_KillsInstead()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Running = false, Launching = true }
        };
        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((Process)null);
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        await _sut.StopServerAsync(server);

        server.ProcessId.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        server.Status.Launching.Should().BeFalse();
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(new Process[2]);

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
    public async Task HandleShutdownCompleteAsync_ClearsStateAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ApiPort = 2303,
            ProcessId = 1234,
            HeadlessClientProcessIds = [5001],
            Status = new GameServerStatus { Running = true }
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((Process)null);
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        await _sut.HandleShutdownCompleteAsync(2303);

        server.ProcessId.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        server.HeadlessClientProcessIds.Should().BeEmpty();
        _mockContext.Verify(x => x.Replace(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.InstanceCount == 0)), Times.Once);
    }

    [Fact]
    public async Task HandleShutdownCompleteAsync_WhenNoMatchingServer_LogsWarning()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns((DomainGameServer)null);

        await _sut.HandleShutdownCompleteAsync(9999);

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("9999"))), Times.Once);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
    }

    [Fact]
    public async Task HandleShutdownCompleteAsync_FinalisesActiveSession()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ApiPort = 2303,
            ProcessId = null,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { CurrentMissionSessionId = "session-abc" }
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        await _sut.HandleShutdownCompleteAsync(2303);

        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("session-abc"), Times.Once);
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(new Process[1]);

        var data = new Dictionary<string, object> { { "map", "Altis" }, { "mission", "test_mission" } };

        await _sut.HandleServerStatusAsync(2303, data);

        server.Status.Running.Should().BeTrue();
        server.Status.Map.Should().Be("Altis");
        server.Status.Mission.Should().Be("test_mission");
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        var result = await _sut.GetAllServerStatusesAsync();

        result[0].Status.Running.Should().BeFalse();
        result[0].ProcessId.Should().BeNull();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Once);
    }

    [Fact]
    public async Task GetAllServerStatusesAsync_WhenSkipFeatureEnabled_ReplacesWithoutStatusCheck()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1", Status = new GameServerStatus { Running = true } } };
        _mockContext.Setup(x => x.Get()).Returns(servers);
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(true);

        var result = await _sut.GetAllServerStatusesAsync();

        result.Should().HaveCount(1);
        result[0].Status.Running.Should().BeTrue();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Once);
        _mockHelpers.Verify(x => x.GetArmaProcesses(), Times.Never);
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        await _sut.GetAllServerStatusesAsync();

        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync("session-1"), Times.Once);
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(new Process[] { null! });
        _mockHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([new ProcessCommandLineInfo(5678, "-config=ServerConfigs/Main.cfg -port=2302 ")]);

        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.RequestTimeout);
        var httpClient = new HttpClient(mockHandler);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _sut.GetAllServerStatusesAsync();

        result.Should().HaveCount(1);
        result[0].ProcessId.Should().Be(5678);
        mockHandler.RequestCount.Should().Be(1);
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(new Process[] { null! });
        _mockHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([]);

        var result = await _sut.GetAllServerStatusesAsync();

        result[0].Status.Running.Should().BeFalse();
        result[0].ProcessId.Should().BeNull();
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(new Process[1]);

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
    public async Task HandleShutdownCompleteAsync_LogsCompletionMessage()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Main",
            ApiPort = 2303,
            ProcessId = null,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus()
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        await _sut.HandleShutdownCompleteAsync(2303);

        _mockLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Main") && s.Contains("2303"))), Times.Once);
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
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        _sut.EnsureMonitorRunning();
        await Task.Delay(1000);

        server.ProcessId.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.Is<GameServerUpdate>(u => u.Server.Id == "s1")), Times.Once);
    }

    [Fact]
    public async Task Monitor_WhenNoServersAndNoOrphanedProcesses_Exits()
    {
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<Process>());

        _sut.EnsureMonitorRunning();
        await Task.Delay(500);

        // Should exit cleanly and allow restart
        _sut.EnsureMonitorRunning();
    }

    [Fact]
    public async Task Monitor_WhenOrphanedProcesses_PushesInstanceCountWhenTheyDie()
    {
        var instanceCount = 2;
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());
        _mockHelpers.Setup(x => x.GetArmaProcesses()).Returns(() => instanceCount > 0 ? new Process[instanceCount] : Array.Empty<Process>());

        _sut.EnsureMonitorRunning();
        await Task.Delay(500);

        instanceCount = 0;
        await Task.Delay(3000);

        _mockServersClient.Verify(x => x.ReceiveInstanceCount(0), Times.Once);
    }
}
