using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using MassTransit;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Converters;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerEventHandlerTests
{
    private readonly Mock<IGameServerProcessManager> _mockProcessManager = new();
    private readonly Mock<IGameServersContext> _mockContext = new();
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint = new();
    private readonly Mock<IMissionStatsService> _mockMissionStatsService = new();
    private readonly Mock<IPerformanceService> _mockPerformanceService = new();
    private readonly Mock<IPersistenceSessionsService> _mockPersistenceSessionsService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly GameServerEventHandler _sut;

    public GameServerEventHandlerTests()
    {
        _sut = new GameServerEventHandler(
            _mockProcessManager.Object,
            _mockContext.Object,
            _mockPublishEndpoint.Object,
            _mockMissionStatsService.Object,
            _mockPerformanceService.Object,
            _mockPersistenceSessionsService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task HandleEventAsync_ShutdownComplete_DelegatesToProcessManager()
    {
        var evt = new GameServerEvent
        {
            Type = "shutdown_complete",
            ApiPort = 2303,
            Data = new Dictionary<string, object>()
        };

        await _sut.HandleEventAsync(evt);

        _mockProcessManager.Verify(x => x.HandleShutdownCompleteAsync(2303), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_ServerStatus_DelegatesToProcessManager()
    {
        var data = new Dictionary<string, object> { { "map", "Altis" } };
        var evt = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 2303,
            Data = data
        };

        await _sut.HandleEventAsync(evt);

        _mockProcessManager.Verify(x => x.HandleServerStatusAsync(2303, data), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_UnknownType_LogsWarning()
    {
        var evt = new GameServerEvent { Type = "unknown_event", Data = new Dictionary<string, object>() };

        await _sut.HandleEventAsync(evt);

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("unknown_event"))), Times.Once);
    }

    // Reproduces the production wire format that the controller would receive from the
    // Rust extension. Deserialises with the same JsonSerializerOptions configuration as
    // Program.cs (PropertyNameCaseInsensitive + InferredTypeConverter), then runs the
    // event handler. Verifies that nested array data inside Dictionary<string, object>
    // survives the InferredTypeConverter round-trip and reaches the performance service.
    [Fact]
    public async Task HandleEventAsync_Performance_PassesParsedDataToPerformanceService()
    {
        const string json = """
                            {
                                "apiPort": 2303,
                                "type": "performance",
                                "data": {
                                    "sessionId": "test-session-id",
                                    "timestamp": "2026-04-04T18:15:17Z",
                                    "server": [60, 59, 61],
                                    "headlessClients": [{"name": "HC1", "fps": [55, 56]}],
                                    "players": [{"uid": "76561198000000000", "fps": [70, 72]}]
                                }
                            }
                            """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new InferredTypeConverter() }
        };

        var evt = JsonSerializer.Deserialize<GameServerEvent>(json, options);

        await _sut.HandleEventAsync(evt);

        _mockPerformanceService.Verify(
            x => x.HandlePerformanceEventAsync(
                "test-session-id",
                It.Is<List<int>>(l => l.SequenceEqual(new[] { 60, 59, 61 })),
                It.Is<List<HeadlessClientPerformance>>(l => l.Count == 1 && l[0].Name == "HC1" && l[0].Fps.SequenceEqual(new[] { 55, 56 })),
                It.Is<List<PlayerPerformance>>(l => l.Count == 1 && l[0].Uid == "76561198000000000" && l[0].Fps.SequenceEqual(new[] { 70, 72 }))
            ),
            Times.Once
        );
    }

    // End-to-end-ish: replaces the mocked performance service with a real one and only mocks
    // the mongo data contexts. Verifies the full chain from JSON wire format through to the
    // mongo Update call, including the UpdateDefinition construction. If this test passes
    // but production still produces empty arrays, the bug must be in the SQF send path or
    // network/extension layer, not in any C# code.
    [Fact]
    public async Task HandleEventAsync_Performance_RealServiceProducesMongoUpdate()
    {
        var mockSessionsContext = new Mock<IMissionSessionsContext>();
        var mockPlayerStatsContext = new Mock<IPlayerMissionStatsContext>();

        var session = new MissionSession { SessionId = "test-session-id", MissionStarted = DateTime.UtcNow.AddMinutes(-5) };
        mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns((PlayerMissionStats)null);

        var realPerformanceService = new PerformanceService(mockSessionsContext.Object, mockPlayerStatsContext.Object);

        var sut = new GameServerEventHandler(
            _mockProcessManager.Object,
            _mockContext.Object,
            _mockPublishEndpoint.Object,
            _mockMissionStatsService.Object,
            realPerformanceService,
            _mockPersistenceSessionsService.Object,
            _mockLogger.Object
        );

        const string json = """
                            {
                                "apiPort": 2303,
                                "type": "performance",
                                "data": {
                                    "sessionId": "test-session-id",
                                    "timestamp": "2026-04-04T18:15:17Z",
                                    "server": [60, 59, 61],
                                    "headlessClients": [{"name": "HC1", "fps": [55, 56]}],
                                    "players": [{"uid": "76561198000000000", "fps": [70, 72]}]
                                }
                            }
                            """;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new InferredTypeConverter() }
        };
        var evt = JsonSerializer.Deserialize<GameServerEvent>(json, options);

        await sut.HandleEventAsync(evt);

        // Verify the real PerformanceService produced an Update call against mongo
        mockSessionsContext.Verify(x => x.Update(session.Id, It.IsAny<UpdateDefinition<MissionSession>>()), Times.Once);

        // Verify rolling player stats was added (new player path)
        mockPlayerStatsContext.Verify(
            x => x.Add(
                It.Is<PlayerMissionStats>(p => p.MissionSessionId == "test-session-id" &&
                                               p.PlayerUid == "76561198000000000" &&
                                               p.FpsSampleCount == 2 &&
                                               p.FpsSampleSum == 142 &&
                                               p.FpsMin == 70 &&
                                               p.FpsMax == 72
                )
            ),
            Times.Once
        );

        // No exception should have been logged
        _mockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }
}
