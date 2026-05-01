using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MassTransit;
using MongoDB.Bson;
using Moq;
using UKSF.Api.ArmaServer.Consumers;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Consumers;

public class ProcessMissionStatsBatchConsumerTests
{
    private readonly Mock<IMissionStatsService> _missionStatsService = new();
    private readonly Mock<IRawEventStore> _rawEventStore = new();
    private readonly Mock<IStatsEventProcessor> _mockShotProcessor = new();
    private readonly Mock<IStatsEventProcessor> _mockHitProcessor = new();
    private readonly Mock<IStatsEventProcessor> _mockKillProcessor = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly ProcessMissionStatsBatchConsumer _consumer;

    private static readonly MissionSession TestSession = new()
    {
        Id = "session-1",
        SessionId = "session-123",
        Mission = "test_mission",
        Map = "test_map"
    };

    public ProcessMissionStatsBatchConsumerTests()
    {
        _mockShotProcessor.Setup(x => x.EventType).Returns("shot");
        _mockHitProcessor.Setup(x => x.EventType).Returns("hit");
        _mockKillProcessor.Setup(x => x.EventType).Returns("kill");

        var processors = new IStatsEventProcessor[] { _mockShotProcessor.Object, _mockHitProcessor.Object, _mockKillProcessor.Object };

        _missionStatsService.Setup(x => x.GetOrCreateSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                            .ReturnsAsync(TestSession);
        _rawEventStore.Setup(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<List<BsonDocument>>())).Returns(Task.CompletedTask);

        _consumer = new ProcessMissionStatsBatchConsumer(_missionStatsService.Object, _rawEventStore.Object, processors, _logger.Object);
    }

    private static Mock<ConsumeContext<ProcessMissionStatsBatch>> CreateContext(ProcessMissionStatsBatch message)
    {
        var context = new Mock<ConsumeContext<ProcessMissionStatsBatch>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_ShouldFindOrCreateSession()
    {
        var receivedAt = new DateTime(2025, 6, 14, 20, 0, 0, DateTimeKind.Utc);
        var message = new ProcessMissionStatsBatch
        {
            SessionId = "session-123",
            Mission = "test_mission",
            Map = "test_map",
            Events = [],
            ReceivedAt = receivedAt
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.GetOrCreateSessionAsync(It.IsAny<string>(), "test_mission", "test_map", receivedAt), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldStoreRawEvents()
    {
        var receivedAt = new DateTime(2025, 6, 14, 20, 0, 0, DateTimeKind.Utc);
        var events = new List<string> { """{"type":"shot","uid":"player1"}""" };
        var message = new ProcessMissionStatsBatch
        {
            SessionId = "session-123",
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = receivedAt
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _rawEventStore.Verify(
            x => x.StoreAsync("session-123", It.Is<List<BsonDocument>>(e => e.Count == 1 && e[0].GetValue("type").AsString == "shot")),
            Times.Once
        );
    }

    [Fact]
    public async Task Consume_ShouldProcessPlayerEvents_GroupedByUid()
    {
        var events = new List<string>
        {
            """{"type":"shot","uid":"player1","weapon":"rifle","fireMode":"single"}""",
            """{"type":"shot","uid":"player1","weapon":"rifle","fireMode":"single"}""",
            """{"type":"hit","uid":"player2","weapon":"pistol","bodyPart":"head","distance":50}"""
        };
        var message = new ProcessMissionStatsBatch
        {
            SessionId = "session-123",
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _mockShotProcessor.Verify(x => x.ProcessForPlayer(It.IsAny<BsonDocument>(), It.IsAny<PlayerMissionStats>()), Times.Exactly(2));
        _mockHitProcessor.Verify(x => x.ProcessForPlayer(It.IsAny<BsonDocument>(), It.IsAny<PlayerMissionStats>()), Times.Once);

        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync("session-123", "player1", It.IsAny<PlayerMissionStats>()), Times.Once);
        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync("session-123", "player2", It.IsAny<PlayerMissionStats>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldProcessMissionStats_WithVehiclesDestroyed()
    {
        var events = new List<string>
        {
            """{"type":"kill","killerUid":"player1","targetType":"vehicle","weapon":"at4"}""",
            """{"type":"kill","killerUid":"player1","targetType":"vehicle","weapon":"at4"}""",
            """{"type":"kill","killerUid":"player2","targetType":"infantry","weapon":"rifle"}"""
        };
        var message = new ProcessMissionStatsBatch
        {
            SessionId = "session-123",
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.UpdateMissionStatsAsync("session-123", It.Is<MissionStats>(s => s.VehiclesDestroyed == 2)), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldHandleEventsWithoutUid_NoPlayerUpdate()
    {
        var events = new List<string> { """{"type":"shot","weapon":"rifle","fireMode":"single"}""" };
        var message = new ProcessMissionStatsBatch
        {
            SessionId = "session-123",
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PlayerMissionStats>()), Times.Never);
        _mockShotProcessor.Verify(x => x.ProcessForPlayer(It.IsAny<BsonDocument>(), It.IsAny<PlayerMissionStats>()), Times.Never);

        _missionStatsService.Verify(x => x.UpdateMissionStatsAsync(It.IsAny<string>(), It.IsAny<MissionStats>()), Times.Never);
    }

    [Fact]
    public async Task Consume_ShouldHandleUnknownEventTypes_NoMissionStatsUpdate()
    {
        var events = new List<string> { """{"type":"explosion","uid":"player1"}""" };
        var message = new ProcessMissionStatsBatch
        {
            SessionId = "session-123",
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PlayerMissionStats>()), Times.Never);

        _missionStatsService.Verify(x => x.UpdateMissionStatsAsync(It.IsAny<string>(), It.IsAny<MissionStats>()), Times.Never);
    }

    [Fact]
    public async Task Consume_WhenEmptyEventsList_ShouldNotUpdateMissionStats()
    {
        var message = new ProcessMissionStatsBatch
        {
            SessionId = "session-123",
            Mission = "test_mission",
            Map = "test_map",
            Events = [],
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.UpdateMissionStatsAsync(It.IsAny<string>(), It.IsAny<MissionStats>()), Times.Never);
        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PlayerMissionStats>()), Times.Never);
    }

    [Fact]
    public async Task Consume_BatchWithVariousEvents_DoesNotPopulateEventCountsOnMissionStats()
    {
        var message = new ProcessMissionStatsBatch
        {
            SessionId = "session-123",
            Mission = "test_mission",
            Map = "test_map",
            ReceivedAt = DateTime.UtcNow,
            Events =
            [
                """{"type":"shot","uid":"u1","weapon":"w","ammo":"a","category":"ballistic"}""",
                """{"type":"unconscious","uid":"u1"}"""
            ]
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.UpdateMissionStatsAsync(It.IsAny<string>(), It.IsAny<MissionStats>()), Times.Never);
    }

    [Fact]
    public async Task Consume_ShouldParallelizePlayerUpdates()
    {
        var events = new List<string>
        {
            """{"type":"shot","uid":"player1","weapon":"rifle","fireMode":"single"}""",
            """{"type":"shot","uid":"player2","weapon":"pistol","fireMode":"single"}""",
            """{"type":"shot","uid":"player3","weapon":"rifle","fireMode":"single"}"""
        };
        var message = new ProcessMissionStatsBatch
        {
            SessionId = "session-123",
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync("session-123", "player1", It.IsAny<PlayerMissionStats>()), Times.Once);
        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync("session-123", "player2", It.IsAny<PlayerMissionStats>()), Times.Once);
        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync("session-123", "player3", It.IsAny<PlayerMissionStats>()), Times.Once);
    }

    [Fact]
    public async Task Consume_BatchEnqueuedAfterMissionEnded_IsRejectedAndNothingWritten()
    {
        var sessionId = "s-late";
        var missionEnded = new DateTime(2026, 4, 25, 20, 56, 0, DateTimeKind.Utc);
        var enqueueAt = missionEnded.AddSeconds(15);

        _missionStatsService.Setup(x => x.GetOrCreateSessionAsync(sessionId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                            .ReturnsAsync(new MissionSession { SessionId = sessionId, MissionEnded = missionEnded });

        var batch = new ProcessMissionStatsBatch
        {
            SessionId = sessionId,
            Mission = "m",
            Map = "k",
            ReceivedAt = enqueueAt.AddSeconds(2),
            EnqueueAt = enqueueAt,
            Events = ["{\"type\":\"shot\",\"uid\":\"u1\"}"]
        };

        var context = CreateContext(batch);

        await _consumer.Consume(context.Object);

        _rawEventStore.Verify(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<List<BsonDocument>>()), Times.Never);
        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PlayerMissionStats>()), Times.Never);
    }

    [Fact]
    public async Task Consume_BatchWithDefaultEnqueueAt_IsNotRejectedWhenMissionEnded()
    {
        var sessionId = "s-okay";
        _missionStatsService.Setup(x => x.GetOrCreateSessionAsync(sessionId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                            .ReturnsAsync(new MissionSession { SessionId = sessionId, MissionEnded = new DateTime(2026, 4, 25, 20, 56, 0, DateTimeKind.Utc) });

        var batch = new ProcessMissionStatsBatch
        {
            SessionId = sessionId,
            Mission = "m",
            Map = "k",
            ReceivedAt = DateTime.UtcNow,
            Events = ["{\"type\":\"shot\",\"uid\":\"u1\"}"]
        };

        var context = CreateContext(batch);

        await _consumer.Consume(context.Object);

        _rawEventStore.Verify(x => x.StoreAsync(sessionId, It.IsAny<List<BsonDocument>>()), Times.Once);
    }

    [Fact]
    public async Task Consume_KillEventWithAssistsArray_IgnoresAssistsField()
    {
        var batch = new ProcessMissionStatsBatch
        {
            SessionId = "s",
            Mission = "m",
            Map = "k",
            ReceivedAt = DateTime.UtcNow,
            Events =
            [
                "{\"type\":\"kill\",\"killerUid\":\"k1\",\"indirect\":false,\"targetType\":\"infantry\"," +
                "\"targetClassname\":\"O_Soldier_F\",\"weapon\":\"\",\"ammo\":\"\"," +
                "\"assists\":[{\"uid\":\"a1\",\"totalDamage\":15.5}]}"
            ]
        };

        var statsByUid = new Dictionary<string, PlayerMissionStats>();
        _missionStatsService.Setup(x => x.UpdatePlayerStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PlayerMissionStats>()))
                            .Callback<string, string, PlayerMissionStats>((_, uid, stats) => statsByUid[uid] = stats)
                            .Returns(Task.CompletedTask);

        var context = CreateContext(batch);

        await _consumer.Consume(context.Object);

        statsByUid.Should().ContainKey("k1");
        statsByUid.Should().NotContainKey("a1");
    }
}
