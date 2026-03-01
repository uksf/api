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
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly ProcessMissionStatsBatchConsumer _consumer;

    private static readonly MissionSession TestSession = new()
    {
        Id = "session-1",
        Mission = "test_mission",
        Map = "test_map"
    };

    public ProcessMissionStatsBatchConsumerTests()
    {
        var processors = new IStatsEventProcessor[] { new ShotEventProcessor(), new HitEventProcessor() };

        _missionStatsService.Setup(x => x.FindOrCreateSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync(TestSession);
        _missionStatsService.Setup(x => x.StoreRawBatchAsync(
                                       It.IsAny<string>(),
                                       It.IsAny<string>(),
                                       It.IsAny<string>(),
                                       It.IsAny<List<BsonDocument>>(),
                                       It.IsAny<DateTime>()
                                   )
                            )
                            .ReturnsAsync(new MissionStatsBatch());

        _consumer = new ProcessMissionStatsBatchConsumer(_missionStatsService.Object, processors, _logger.Object);
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
            Mission = "test_mission",
            Map = "test_map",
            Events = [],
            ReceivedAt = receivedAt
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.FindOrCreateSessionAsync("test_mission", "test_map", receivedAt), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldStoreRawBatch()
    {
        var receivedAt = new DateTime(2025, 6, 14, 20, 0, 0, DateTimeKind.Utc);
        var events = new List<BsonDocument> { new() { { "type", "shot" }, { "uid", "player1" } } };
        var message = new ProcessMissionStatsBatch
        {
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = receivedAt
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.StoreRawBatchAsync("session-1", "test_mission", "test_map", events, receivedAt), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldProcessPlayerEvents_GroupedByUid()
    {
        var events = new List<BsonDocument>
        {
            new()
            {
                { "type", "shot" },
                { "uid", "player1" },
                { "weapon", "rifle" },
                { "fireMode", "single" }
            },
            new()
            {
                { "type", "shot" },
                { "uid", "player1" },
                { "weapon", "rifle" },
                { "fireMode", "single" }
            },
            new()
            {
                { "type", "hit" },
                { "uid", "player2" },
                { "weapon", "pistol" },
                { "bodyPart", "head" },
                { "distance", 50 }
            }
        };
        var message = new ProcessMissionStatsBatch
        {
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(
            x => x.UpdatePlayerStatsAsync("session-1", "player1", It.Is<PlayerMissionStats>(s => s.TotalShots == 2 && s.WeaponBreakdown.ContainsKey("rifle"))),
            Times.Once
        );

        _missionStatsService.Verify(
            x => x.UpdatePlayerStatsAsync("session-1", "player2", It.Is<PlayerMissionStats>(s => s.TotalHits == 1 && s.BodyPartHits.ContainsKey("head"))),
            Times.Once
        );
    }

    [Fact]
    public async Task Consume_ShouldProcessMissionStats_WithEventCounts()
    {
        var events = new List<BsonDocument>
        {
            new()
            {
                { "type", "shot" },
                { "uid", "player1" },
                { "weapon", "rifle" },
                { "fireMode", "single" }
            },
            new()
            {
                { "type", "hit" },
                { "uid", "player1" },
                { "weapon", "rifle" },
                { "bodyPart", "torso" },
                { "distance", 100 }
            },
            new()
            {
                { "type", "shot" },
                { "uid", "player2" },
                { "weapon", "pistol" },
                { "fireMode", "single" }
            }
        };
        var message = new ProcessMissionStatsBatch
        {
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(
            x => x.UpdateMissionStatsAsync("session-1", It.Is<MissionStats>(s => s.EventCounts["shot"] == 2 && s.EventCounts["hit"] == 1)),
            Times.Once
        );
    }

    [Fact]
    public async Task Consume_ShouldHandleEventsWithoutUid_CountedInMissionStatsButNoPlayerUpdate()
    {
        var events = new List<BsonDocument>
        {
            new()
            {
                { "type", "shot" },
                { "weapon", "rifle" },
                { "fireMode", "single" }
            }
        };
        var message = new ProcessMissionStatsBatch
        {
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PlayerMissionStats>()), Times.Never);

        _missionStatsService.Verify(x => x.UpdateMissionStatsAsync("session-1", It.Is<MissionStats>(s => s.EventCounts["shot"] == 1)), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldHandleUnknownEventTypes_LoggedAndCountedInMissionStats()
    {
        var events = new List<BsonDocument> { new() { { "type", "explosion" }, { "uid", "player1" } } };
        var message = new ProcessMissionStatsBatch
        {
            Mission = "test_mission",
            Map = "test_map",
            Events = events,
            ReceivedAt = DateTime.UtcNow
        };
        var context = CreateContext(message);

        await _consumer.Consume(context.Object);

        _logger.Verify(x => x.LogDebug(It.Is<string>(s => s.Contains("explosion"))), Times.Once);

        _missionStatsService.Verify(x => x.UpdatePlayerStatsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PlayerMissionStats>()), Times.Never);

        _missionStatsService.Verify(x => x.UpdateMissionStatsAsync("session-1", It.Is<MissionStats>(s => s.EventCounts["explosion"] == 1)), Times.Once);
    }
}
