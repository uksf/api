using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.ArmaServer.Consumers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServersServiceTests
{
    private readonly Mock<IGameServerHelpers> _mockGameServerHelpers = new();
    private readonly Mock<IGameServersContext> _mockGameServersContext = new();
    private readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IHubContext<ServersHub, IServersClient>> _mockServersHub = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint = new();
    private readonly Mock<IPersistenceSessionsService> _mockPersistenceSessionsService = new();

    private readonly GameServersService _subject;

    public GameServersServiceTests()
    {
        var mockClients = new Mock<IServersClient>();
        var mockHubClients = new Mock<IHubClients<IServersClient>>();
        mockHubClients.Setup(x => x.All).Returns(mockClients.Object);
        _mockServersHub.Setup(x => x.Clients).Returns(mockHubClients.Object);

        _subject = new GameServersService(
            _mockGameServersContext.Object,
            _mockGameServerHelpers.Object,
            _mockProcessUtilities.Object,
            _mockHttpClientFactory.Object,
            _mockVariablesService.Object,
            _mockServersHub.Object,
            _mockLogger.Object,
            _mockPublishEndpoint.Object,
            _mockPersistenceSessionsService.Object,
            new Mock<IMissionStatsService>().Object,
            new Mock<IPerformanceService>().Object
        );
    }

    [Fact]
    public async Task KillGameServer_Should_kill_server_successfully()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-456",
            LaunchedBy = "previous-user-123",
            ProcessId = 1234,
            HeadlessClientProcessIds = []
        };

        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((System.Diagnostics.Process)null);

        await _subject.KillGameServer(gameServer);

        gameServer.ProcessId.Should().BeNull();
        gameServer.HeadlessClientProcessIds.Count.Should().Be(0);
        gameServer.Status.Running.Should().BeFalse();
        gameServer.Status.Launching.Should().BeFalse();
        gameServer.Status.Stopping.Should().BeFalse();
        _mockProcessUtilities.Verify(x => x.FindProcessById(1234), Times.Once);
    }

    [Fact]
    public async Task KillGameServer_ShouldClearLaunchedBy()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-456",
            LaunchedBy = "user-123",
            ProcessId = 1234,
            HeadlessClientProcessIds = []
        };

        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((System.Diagnostics.Process)null);

        await _subject.KillGameServer(gameServer);

        gameServer.LaunchedBy.Should().BeNull();
    }

    [Fact]
    public async Task KillGameServer_Should_handle_null_process_id_gracefully()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-456",
            LaunchedBy = "previous-user-123",
            ProcessId = null,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Running = true, Stopping = true }
        };

        await _subject.KillGameServer(gameServer);

        gameServer.ProcessId.Should().BeNull();
        gameServer.LaunchedBy.Should().BeNull();
        gameServer.Status.Running.Should().BeFalse();
        gameServer.Status.Stopping.Should().BeFalse();
        _mockProcessUtilities.Verify(x => x.FindProcessById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task KillAllArmaProcesses_Should_clear_process_data_and_reset_status()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server1",
                LaunchedBy = "user1",
                ProcessId = 1001,
                HeadlessClientProcessIds = [],
                Status = new GameServerStatus { Running = true, Mission = "test.Altis" }
            },
            new()
            {
                Id = "server2",
                LaunchedBy = "user2",
                ProcessId = 1002,
                HeadlessClientProcessIds = [],
                Status = new GameServerStatus { Running = true, Stopping = true }
            },
            new()
            {
                Id = "server3",
                LaunchedBy = null,
                ProcessId = null,
                HeadlessClientProcessIds = []
            }
        };

        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns([]);

        var result = await _subject.KillAllArmaProcesses();

        gameServers.Should()
                   .AllSatisfy(server =>
                       {
                           server.ProcessId.Should().BeNull();
                           server.HeadlessClientProcessIds.Count.Should().Be(0);
                           server.Status.Running.Should().BeFalse();
                           server.Status.Stopping.Should().BeFalse();
                           server.Status.Launching.Should().BeFalse();
                           server.Status.Mission.Should().BeNull();
                       }
                   );
        result.Should().Be(0);
    }

    [Fact]
    public async Task KillAllArmaProcesses_ShouldClearLaunchedBy()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server1",
                LaunchedBy = "user1",
                ProcessId = 1001,
                HeadlessClientProcessIds = []
            },
            new()
            {
                Id = "server2",
                LaunchedBy = "user2",
                ProcessId = 1002,
                HeadlessClientProcessIds = []
            }
        };

        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns([]);

        await _subject.KillAllArmaProcesses();

        gameServers.Should().AllSatisfy(server => server.LaunchedBy.Should().BeNull());
    }

    [Fact]
    public async Task GetAllGameServerStatuses_WhenNoArmaProcesses_ShouldSetAllNotRunningWithoutHttpCalls()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server-1",
                Port = 2302,
                ProcessId = 1234
            },
            new()
            {
                Id = "server-2",
                Port = 2402,
                ProcessId = 5678
            }
        };
        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns([]);

        var result = await _subject.GetAllGameServerStatuses();

        result.Should()
              .AllSatisfy(server =>
                  {
                      server.Status.Running.Should().BeFalse();
                      server.Status.Launching.Should().BeFalse();
                      server.ProcessId.Should().BeNull();
                  }
              );
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
        _mockGameServerHelpers.Verify(x => x.GetArmaProcessesWithCommandLine(), Times.Never);
    }

    [Fact]
    public async Task GetAllGameServerStatuses_WhenNoArmaProcesses_ShouldSkipWmiQuery()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server-1",
                Port = 2302,
                ProcessId = 1234
            },
            new()
            {
                Id = "server-2",
                Port = 2402,
                ProcessId = 5678
            }
        };
        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns([]);

        var result = await _subject.GetAllGameServerStatuses();

        result.Should()
              .AllSatisfy(server =>
                  {
                      server.Status.Running.Should().BeFalse();
                      server.ProcessId.Should().BeNull();
                  }
              );
        _mockGameServerHelpers.Verify(x => x.GetArmaProcessesWithCommandLine(), Times.Never);
    }

    [Fact]
    public async Task GetAllGameServerStatuses_WhenArmaProcesses_ShouldOnlyHttpCallMatchingServers()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server-1",
                Port = 2302,
                ApiPort = 2303
            },
            new()
            {
                Id = "server-2",
                Port = 2402,
                ApiPort = 2403
            }
        };
        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns(new System.Diagnostics.Process[] { null! });
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine())
                              .Returns([new ProcessCommandLineInfo(5678, "-config=ServerConfigs/Main.cfg -port=2302 -apiport=\"2303\"")]);

        var mockHandler = new MockHttpMessageHandler(System.Net.HttpStatusCode.RequestTimeout);
        var httpClient = new HttpClient(mockHandler);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _subject.GetAllGameServerStatuses();

        var server2 = result.Find(s => s.Id == "server-2");
        server2!.Status.Running.Should().BeFalse();
        server2.Status.Launching.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllGameServerStatuses_WhenNoMatchingProcess_ShouldResetStaleStatusFields()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server-1",
                Port = 2302,
                ApiPort = 2303,
                Status = new GameServerStatus
                {
                    Running = true,
                    Mission = "mission.Altis.pbo",
                    Map = "Altis",
                    MaxPlayers = "32",
                    LastEventReceived = DateTime.UtcNow
                }
            }
        };
        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns(new System.Diagnostics.Process[] { null! });
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([]);

        var result = await _subject.GetAllGameServerStatuses();

        var server = result.Find(s => s.Id == "server-1");
        server!.Status.Running.Should().BeFalse();
        server.Status.Mission.Should().BeNull();
        server.Status.Map.Should().BeNull();
        server.Status.MaxPlayers.Should().BeNull();
        server.Status.LastEventReceived.Should().Be(default);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenShutdownComplete_ShouldResetStatus()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            Name = "Main",
            ApiPort = 2303,
            HeadlessClientProcessIds = [5001],
            Status = new GameServerStatus
            {
                Running = true,
                Mission = "mission.Altis.pbo",
                Map = "Altis",
                MaxPlayers = "32",
                LastEventReceived = DateTime.UtcNow
            }
        };

        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(gameServer);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((System.Diagnostics.Process)null);

        var gameServerEvent = new GameServerEvent
        {
            Type = "shutdown_complete",
            ApiPort = 2303,
            Data = new Dictionary<string, object>()
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        gameServer.Status.Running.Should().BeFalse();
        gameServer.Status.Mission.Should().BeNull();
        gameServer.Status.Map.Should().BeNull();
        gameServer.Status.MaxPlayers.Should().BeNull();
        gameServer.Status.LastEventReceived.Should().Be(default);
        gameServer.HeadlessClientProcessIds.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenMissionStats_ShouldPublishToMassTransit()
    {
        var eventsJson = JsonSerializer.Deserialize<JsonElement>(
            """[{"type":"shot","uid":"123","weapon":"arifle_MX_F","magazine":"30Rnd","fireMode":"Single"}]"""
        );

        var gameServerEvent = new GameServerEvent
        {
            Type = "mission_stats",
            Data = new Dictionary<string, object>
            {
                { "sessionId", "session-123" },
                { "mission", "test.Altis" },
                { "map", "Altis" },
                { "events", eventsJson }
            }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockPublishEndpoint.Verify(
            x => x.Publish(
                It.Is<ProcessMissionStatsBatch>(m => m.Mission == "test.Altis" && m.Map == "Altis" && m.Events.Count == 1),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenMissionStatsMissingData_ShouldNotPublish()
    {
        var gameServerEvent = new GameServerEvent { Type = "mission_stats", Data = new Dictionary<string, object> { { "map", "Altis" } } };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<ProcessMissionStatsBatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenServerStatus_ShouldUpdateStatusCacheForRunningServers()
    {
        var runningServer = new DomainGameServer
        {
            Id = "server-1",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = 1234
        };
        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>()))
                               .Returns((Func<DomainGameServer, bool> predicate) => new List<DomainGameServer> { runningServer }.FirstOrDefault(predicate));
        _mockGameServerHelpers.Setup(x => x.GetMaxPlayerCountFromConfig(runningServer)).Returns("64");
        _mockGameServerHelpers.Setup(x => x.StripMilliseconds(It.IsAny<TimeSpan>())).Returns(TimeSpan.FromSeconds(120));
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<System.Diagnostics.Process>());

        var gameServerEvent = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 2303,
            Data = new Dictionary<string, object>
            {
                { "map", "Altis" },
                { "mission", "co40_op_eagle.Altis" },
                { "players", "12" },
                { "uptime", "120.5" }
            }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockServersHub.Verify(x => x.Clients.All.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenServerStatusWithUptime_ShouldSetStartedAt()
    {
        var runningServer = new DomainGameServer
        {
            Id = "server-1",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = 1234
        };
        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>()))
                               .Returns((Func<DomainGameServer, bool> predicate) => new List<DomainGameServer> { runningServer }.FirstOrDefault(predicate));
        _mockGameServerHelpers.Setup(x => x.GetMaxPlayerCountFromConfig(runningServer)).Returns("64");
        _mockGameServerHelpers.Setup(x => x.StripMilliseconds(It.IsAny<TimeSpan>())).Returns(TimeSpan.FromSeconds(120));
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<System.Diagnostics.Process>());

        var gameServerEvent = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 2303,
            Data = new Dictionary<string, object> { { "uptime", "120.5" } }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        runningServer.Status.StartedAt.Should().NotBeNull();
        runningServer.Status.StartedAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(-120.5), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenServerStatusWithUptime_ShouldNotOverwriteExistingStartedAt()
    {
        _subject.ClearStatusCache("server-started-at");
        var existingStartedAt = DateTime.UtcNow.AddHours(-1);
        var runningServer = new DomainGameServer
        {
            Id = "server-started-at",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = 1234,
            Status = new GameServerStatus { StartedAt = existingStartedAt }
        };
        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>()))
                               .Returns((Func<DomainGameServer, bool> predicate) => new List<DomainGameServer> { runningServer }.FirstOrDefault(predicate));
        _mockGameServerHelpers.Setup(x => x.GetMaxPlayerCountFromConfig(runningServer)).Returns("64");
        _mockGameServerHelpers.Setup(x => x.StripMilliseconds(It.IsAny<TimeSpan>())).Returns(TimeSpan.FromSeconds(3600));
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<System.Diagnostics.Process>());

        var gameServerEvent = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 2303,
            Data = new Dictionary<string, object> { { "uptime", "3600" } }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        runningServer.Status.StartedAt.Should().Be(existingStartedAt);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenServerStatus_ShouldNotCrashWithNoRunningServers()
    {
        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns((DomainGameServer)null);

        var gameServerEvent = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 9999,
            Data = new Dictionary<string, object> { { "map", "Altis" }, { "players", "5" } }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("9999"))), Times.Once);
        _mockServersHub.Verify(x => x.Clients.All.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Never);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenServerStatusWithPerformanceData_ShouldUpdateCacheForRunningServers()
    {
        var runningServer = new DomainGameServer
        {
            Id = "server-perf-1",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = 1234
        };
        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>()))
                               .Returns((Func<DomainGameServer, bool> predicate) => new List<DomainGameServer> { runningServer }.FirstOrDefault(predicate));
        _mockGameServerHelpers.Setup(x => x.GetMaxPlayerCountFromConfig(runningServer)).Returns("64");
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<System.Diagnostics.Process>());

        var gameServerEvent = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 2303,
            Data = new Dictionary<string, object>
            {
                { "entityCount", "1500" },
                { "aiCount", "200" },
                { "headlessClientCount", "2" }
            }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockServersHub.Verify(x => x.Clients.All.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenServerStatusWithBadData_ShouldNotThrow()
    {
        var runningServer = new DomainGameServer
        {
            Id = "server-1",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = 1234
        };
        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>()))
                               .Returns((Func<DomainGameServer, bool> predicate) => new List<DomainGameServer> { runningServer }.FirstOrDefault(predicate));
        _mockGameServerHelpers.Setup(x => x.GetMaxPlayerCountFromConfig(runningServer)).Throws(new Exception("Config error"));

        var gameServerEvent = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 2303,
            Data = new Dictionary<string, object> { { "players", "5" } }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("server_status")), It.IsAny<Exception>()), Times.Once);
        _mockServersHub.Verify(x => x.Clients.All.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Never);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenUnknownType_ShouldLogWarning()
    {
        var gameServerEvent = new GameServerEvent { Type = "unknown_event", Data = new Dictionary<string, object>() };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("unknown_event"))), Times.Once);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenEventHandlerThrows_ShouldLogError()
    {
        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Throws(new Exception("DB error"));

        var gameServerEvent = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 2303,
            Data = new Dictionary<string, object> { { "players", "5" } }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("server_status")), It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenServerStatus_ShouldTargetSpecificServerByApiPort()
    {
        var server1 = new DomainGameServer
        {
            Id = "server-1",
            ApiPort = 2303,
            ProcessId = 1234
        };
        var server2 = new DomainGameServer
        {
            Id = "server-2",
            ApiPort = 2403,
            ProcessId = 5678
        };

        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>()))
                               .Returns((Func<DomainGameServer, bool> predicate) => new List<DomainGameServer> { server1, server2 }.FirstOrDefault(predicate));
        _mockGameServerHelpers.Setup(x => x.GetMaxPlayerCountFromConfig(It.IsAny<DomainGameServer>())).Returns("64");
        _mockGameServerHelpers.Setup(x => x.StripMilliseconds(It.IsAny<TimeSpan>())).Returns(TimeSpan.FromSeconds(120));
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns(Array.Empty<System.Diagnostics.Process>());

        var gameServerEvent = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 2303,
            Data = new Dictionary<string, object> { { "map", "Altis" }, { "uptime", "120.5" } }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockGameServersContext.Verify(x => x.Replace(It.Is<DomainGameServer>(s => s.Id == "server-1")), Times.Once);
        _mockGameServersContext.Verify(x => x.Replace(It.Is<DomainGameServer>(s => s.Id == "server-2")), Times.Never);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenMissionStatsMissingMission_ShouldNotPublish()
    {
        var gameServerEvent = new GameServerEvent
        {
            Type = "mission_stats", Data = new Dictionary<string, object> { { "map", "Altis" }, { "events", new List<object>() } }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<ProcessMissionStatsBatch>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("missing sessionId"))), Times.Once);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenPersistenceSave_ShouldDelegateToService()
    {
        var gameServerEvent = new GameServerEvent
        {
            Type = "persistence_save",
            Data = new Dictionary<string, object>
            {
                { "id", "chunk-123" },
                { "key", "session-key" },
                { "sessionId", "session-456" },
                { "index", 0 },
                { "total", 1 },
                { "data", "{\"key\":\"test\"}" }
            }
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockPersistenceSessionsService.Verify(
            x => x.HandleSaveChunkAsync(
                It.Is<ChunkEnvelope>(c => c.Id == "chunk-123" &&
                                          c.Key == "session-key" &&
                                          c.SessionId == "session-456" &&
                                          c.Index == 0 &&
                                          c.Total == 1 &&
                                          c.Data == "{\"key\":\"test\"}"
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenPersistenceSave_WithMissingFields_ShouldUseDefaults()
    {
        var gameServerEvent = new GameServerEvent { Type = "persistence_save", Data = new Dictionary<string, object> { { "key", "session-key" } } };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockPersistenceSessionsService.Verify(
            x => x.HandleSaveChunkAsync(It.Is<ChunkEnvelope>(c => c.Id == string.Empty && c.Index == 0 && c.Total == 1 && c.Data == string.Empty)),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenShutdownComplete_ShouldKillHeadlessClients()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            Name = "Main",
            ApiPort = 2303,
            HeadlessClientProcessIds = [5001, 5002]
        };

        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(gameServer);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((System.Diagnostics.Process)null);

        var gameServerEvent = new GameServerEvent
        {
            Type = "shutdown_complete",
            ApiPort = 2303,
            Data = new Dictionary<string, object>()
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockProcessUtilities.Verify(x => x.FindProcessById(5001), Times.Once);
        _mockProcessUtilities.Verify(x => x.FindProcessById(5002), Times.Once);
        gameServer.HeadlessClientProcessIds.Should().BeEmpty();
        _mockGameServersContext.Verify(x => x.Replace(gameServer), Times.Once);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenShutdownComplete_WithNoMatchingServer_ShouldLogWarning()
    {
        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns((DomainGameServer)null);

        var gameServerEvent = new GameServerEvent
        {
            Type = "shutdown_complete",
            ApiPort = 2303,
            Data = new Dictionary<string, object>()
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("2303"))), Times.Once);
        _mockProcessUtilities.Verify(x => x.FindProcessById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task HandleGameServerEvent_WhenShutdownComplete_WithNoHeadlessClients_ShouldDoNothing()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            Name = "Main",
            ApiPort = 2303,
            HeadlessClientProcessIds = []
        };

        _mockGameServersContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(gameServer);

        var gameServerEvent = new GameServerEvent
        {
            Type = "shutdown_complete",
            ApiPort = 2303,
            Data = new Dictionary<string, object>()
        };

        await _subject.HandleGameServerEvent(gameServerEvent);

        _mockProcessUtilities.Verify(x => x.FindProcessById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task LaunchGameServer_ShouldLaunchServerAndHCsAndSaveState()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            NumberHeadlessClients = 1,
            HeadlessClientProcessIds = []
        };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("server-args");
        _mockGameServerHelpers.Setup(x => x.FormatHeadlessClientLaunchArguments(gameServer, 0)).Returns("hc-args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("test-path");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("test-path", "server-args")).Returns(1001);
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("test-path", "hc-args")).Returns(2001);

        await _subject.LaunchGameServer(gameServer, "mission.Altis.pbo", "user-123");

        gameServer.Status.Launching.Should().BeTrue();
        gameServer.Status.Mission.Should().Be("mission");
        gameServer.Status.Map.Should().Be("Altis");
        gameServer.LaunchedBy.Should().Be("user-123");
        gameServer.ProcessId.Should().Be(1001);
        gameServer.HeadlessClientProcessIds.Should().BeEquivalentTo([2001]);
        _mockGameServersContext.Verify(x => x.Replace(gameServer), Times.Once);
    }

    [Fact]
    public async Task LaunchGameServer_WithMissionWithoutMap_ShouldNotSetMap()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            NumberHeadlessClients = 0,
            HeadlessClientProcessIds = []
        };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("path");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("path", "args")).Returns(1001);

        await _subject.LaunchGameServer(gameServer, "mission.pbo", "user-123");

        gameServer.Status.Mission.Should().Be("mission");
        gameServer.Status.Map.Should().BeNull();
    }

    [Fact]
    public async Task LaunchGameServer_WithNullOptionalParams_ShouldNotSetMissionOrLaunchedBy()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            NumberHeadlessClients = 0,
            HeadlessClientProcessIds = []
        };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("path");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("path", "args")).Returns(1001);

        await _subject.LaunchGameServer(gameServer);

        gameServer.Status.Launching.Should().BeTrue();
        gameServer.Status.Mission.Should().BeNull();
        gameServer.LaunchedBy.Should().BeNull();
    }

    [Fact]
    public async Task LaunchGameServer_WhenServerLaunchFails_ShouldPropagateException()
    {
        var gameServer = new DomainGameServer { Id = "server-1", HeadlessClientProcessIds = [] };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("path");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("path", "args")).Throws(new Exception("Launch failed"));

        var action = () => _subject.LaunchGameServer(gameServer);
        await action.Should().ThrowAsync<Exception>().WithMessage("Launch failed");
    }

    [Fact]
    public async Task LaunchGameServer_WhenHCLaunchFails_ShouldPropagateExceptionWithServerStillRunning()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            NumberHeadlessClients = 1,
            HeadlessClientProcessIds = []
        };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("server-args");
        _mockGameServerHelpers.Setup(x => x.FormatHeadlessClientLaunchArguments(gameServer, 0)).Returns("hc-args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("path");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("path", "server-args")).Returns(1001);
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("path", "hc-args")).Throws(new Exception("HC launch failed"));

        var action = () => _subject.LaunchGameServer(gameServer, "mission.pbo", "user-123");
        await action.Should().ThrowAsync<Exception>().WithMessage("HC launch failed");

        gameServer.ProcessId.Should().Be(1001);
    }

    [Fact]
    public async Task StopGameServer_WhenRunning_ShouldSendShutdownCommand()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            ApiPort = 2303,
            NumberHeadlessClients = 2,
            Status = new GameServerStatus { Running = true }
        };

        var mockHandler = new MockHttpMessageHandler(System.Net.HttpStatusCode.OK);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(mockHandler, disposeHandler: false));

        await _subject.StopGameServer(gameServer);

        mockHandler.RequestCount.Should().Be(1);
        mockHandler.LastRequestUri.Should().Contain("2303");
    }

    [Fact]
    public async Task StopGameServer_WhenNotRunning_ShouldKillInsteadOfShutdown()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            ApiPort = 2303,
            ProcessId = 1001,
            Status = new GameServerStatus { Launching = true, Running = false },
            HeadlessClientProcessIds = [2001]
        };

        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns((System.Diagnostics.Process)null);

        await _subject.StopGameServer(gameServer);

        _mockProcessUtilities.Verify(x => x.FindProcessById(1001), Times.Once);
        gameServer.Status.Running.Should().BeFalse();
        gameServer.ProcessId.Should().BeNull();
        gameServer.HeadlessClientProcessIds.Should().BeEmpty();
        _mockGameServersContext.Verify(x => x.Replace(gameServer), Times.Once);
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }
}

internal class MockHttpMessageHandler(System.Net.HttpStatusCode statusCode) : HttpMessageHandler
{
    public int RequestCount { get; private set; }
    public string LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri?.ToString();
        return Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
