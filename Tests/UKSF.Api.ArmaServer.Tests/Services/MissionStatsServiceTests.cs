using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class MissionStatsServiceTests
{
    private readonly Mock<IMissionSessionsContext> _mockSessionsContext = new();
    private readonly Mock<IMissionStatsBatchesContext> _mockBatchesContext = new();
    private readonly Mock<IPlayerMissionStatsContext> _mockPlayerStatsContext = new();
    private readonly Mock<IMissionStatsContext> _mockMissionStatsContext = new();

    private readonly MissionStatsService _subject;

    public MissionStatsServiceTests()
    {
        _subject = new MissionStatsService(
            _mockSessionsContext.Object,
            _mockBatchesContext.Object,
            _mockPlayerStatsContext.Object,
            _mockMissionStatsContext.Object
        );
    }

    #region GetOrCreateSessionAsync

    [Fact]
    public async Task GetOrCreateSessionAsync_WhenNoMatchingSession_ShouldCreateNewSession()
    {
        var receivedAt = new DateTime(2025, 6, 14, 20, 0, 0); // Saturday
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns((MissionSession)null);

        var result = await _subject.GetOrCreateSessionAsync("session-123", "co40_op_eagle", "Altis", receivedAt);

        result.SessionId.Should().Be("session-123");
        result.Mission.Should().Be("co40_op_eagle");
        result.Map.Should().Be("Altis");
        result.FirstBatchReceived.Should().Be(receivedAt);
        result.LastBatchReceived.Should().Be(receivedAt);
        result.TotalBatchesReceived.Should().Be(1);
        _mockSessionsContext.Verify(x => x.Add(It.IsAny<MissionSession>()), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateSessionAsync_WhenSessionExists_ShouldReturnExistingAndUpdate()
    {
        var now = new DateTime(2025, 6, 14, 20, 0, 0);
        var existingSession = new MissionSession
        {
            SessionId = "session-123",
            Mission = "co40_op_eagle",
            Map = "Altis",
            LastBatchReceived = now.AddHours(-1),
            TotalBatchesReceived = 3
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(existingSession);

        var result = await _subject.GetOrCreateSessionAsync("session-123", "co40_op_eagle", "Altis", now);

        result.Id.Should().Be(existingSession.Id);
        result.TotalBatchesReceived.Should().Be(4);
        result.LastBatchReceived.Should().Be(now);
        _mockSessionsContext.Verify(x => x.Add(It.IsAny<MissionSession>()), Times.Never);
        _mockSessionsContext.Verify(x => x.Update(existingSession.Id, It.IsAny<UpdateDefinition<MissionSession>>()), Times.Once);
    }

    #endregion

    #region StoreRawBatchAsync

    [Fact]
    public async Task StoreRawBatchAsync_ShouldStoreBatchWithSessionReference()
    {
        var sessionId = "session-123";
        var events = new List<BsonDocument> { new() { { "type", "shot" } }, new() { { "type", "hit" } } };
        var receivedAt = new DateTime(2025, 6, 14, 20, 0, 0);

        var result = await _subject.StoreRawBatchAsync(sessionId, "co40_op_eagle", "Altis", events, receivedAt);

        result.MissionSessionId.Should().Be(sessionId);
        result.Mission.Should().Be("co40_op_eagle");
        result.Map.Should().Be("Altis");
        result.Events.Should().HaveCount(2);
        result.ReceivedAt.Should().Be(receivedAt);
        _mockBatchesContext.Verify(x => x.Add(It.IsAny<MissionStatsBatch>()), Times.Once);
    }

    #endregion

    #region UpdatePlayerStatsAsync

    [Fact]
    public async Task UpdatePlayerStatsAsync_WhenNoExistingStats_ShouldCreateNew()
    {
        var sessionId = "session-123";
        var playerUid = "76561198012345678";
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns((PlayerMissionStats)null);

        var updates = new PlayerMissionStats
        {
            TotalShots = 10,
            TotalHits = 5,
            DistanceOnFoot = 1000.5
        };

        await _subject.UpdatePlayerStatsAsync(sessionId, playerUid, updates);

        _mockPlayerStatsContext.Verify(
            x => x.Add(
                It.Is<PlayerMissionStats>(s => s.MissionSessionId == sessionId &&
                                               s.PlayerUid == playerUid &&
                                               s.TotalShots == 10 &&
                                               s.TotalHits == 5 &&
                                               Math.Abs(s.DistanceOnFoot - 1000.5) < 0.01
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdatePlayerStatsAsync_WhenExistingStats_ShouldUseAtomicIncrement()
    {
        var sessionId = "session-123";
        var playerUid = "76561198012345678";
        var existing = new PlayerMissionStats
        {
            MissionSessionId = sessionId,
            PlayerUid = playerUid,
            TotalShots = 10,
            TotalHits = 5,
            DistanceOnFoot = 500
        };
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns(existing);

        var updates = new PlayerMissionStats
        {
            TotalShots = 8,
            TotalHits = 3,
            DistanceOnFoot = 300.5
        };

        await _subject.UpdatePlayerStatsAsync(sessionId, playerUid, updates);

        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
        _mockPlayerStatsContext.Verify(x => x.Replace(It.IsAny<PlayerMissionStats>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePlayerStatsAsync_WhenExistingStats_WithWeaponBreakdown_ShouldUseAtomicUpdate()
    {
        var sessionId = "session-123";
        var playerUid = "76561198012345678";
        var existing = new PlayerMissionStats
        {
            MissionSessionId = sessionId,
            PlayerUid = playerUid,
            WeaponBreakdown = new Dictionary<string, WeaponStats>
            {
                ["rhs_weap_m4a1"] = new()
                {
                    Shots = 5,
                    Hits = 2,
                    FireModes = new Dictionary<string, int> { ["Single"] = 3, ["FullAuto"] = 2 }
                }
            }
        };
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns(existing);

        var updates = new PlayerMissionStats
        {
            WeaponBreakdown = new Dictionary<string, WeaponStats>
            {
                ["rhs_weap_m4a1"] = new()
                {
                    Shots = 3,
                    Hits = 1,
                    FireModes = new Dictionary<string, int> { ["Single"] = 1, ["Burst"] = 2 }
                },
                ["rhs_weap_m249"] = new()
                {
                    Shots = 10,
                    Hits = 4,
                    FireModes = new Dictionary<string, int> { ["FullAuto"] = 10 }
                }
            }
        };

        await _subject.UpdatePlayerStatsAsync(sessionId, playerUid, updates);

        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
    }

    #endregion

    #region HandleMissionEndedAsync — Batch Merging

    [Fact]
    public async Task HandleMissionEndedAsync_ShouldMergeAllBatchesIntoOne()
    {
        var sessionId = "session-123";
        var batch1Events = new List<BsonDocument> { new() { { "type", "shot" }, { "uid", "p1" } } };
        var batch2Events = new List<BsonDocument> { new() { { "type", "hit" }, { "uid", "p1" } }, new() { { "type", "kill" }, { "killerUid", "p1" } } };

        var batch1 = new MissionStatsBatch
        {
            Id = "batch-1",
            MissionSessionId = sessionId,
            Mission = "test_mission",
            Map = "Altis",
            ReceivedAt = new DateTime(2025, 6, 14, 20, 0, 0),
            Events = batch1Events
        };
        var batch2 = new MissionStatsBatch
        {
            Id = "batch-2",
            MissionSessionId = sessionId,
            Mission = "test_mission",
            Map = "Altis",
            ReceivedAt = new DateTime(2025, 6, 14, 20, 1, 0),
            Events = batch2Events
        };

        var session = new MissionSession { SessionId = sessionId };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([batch1, batch2]);

        await _subject.HandleMissionEndedAsync(sessionId, 300, DateTime.UtcNow);

        _mockBatchesContext.Verify(
            x => x.Add(It.Is<MissionStatsBatch>(b => b.MissionSessionId == sessionId && b.Events.Count == 3 && b.ReceivedAt == batch1.ReceivedAt)),
            Times.Once
        );
        _mockBatchesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<MissionStatsBatch, bool>>>()), Times.Once);
    }

    [Fact]
    public async Task HandleMissionEndedAsync_WhenSingleBatch_ShouldNotMerge()
    {
        var sessionId = "session-123";
        var batch = new MissionStatsBatch
        {
            Id = "batch-1",
            MissionSessionId = sessionId,
            Events = [new BsonDocument { { "type", "shot" } }]
        };

        var session = new MissionSession { SessionId = sessionId };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([batch]);

        await _subject.HandleMissionEndedAsync(sessionId, 300, DateTime.UtcNow);

        _mockBatchesContext.Verify(x => x.Add(It.IsAny<MissionStatsBatch>()), Times.Never);
        _mockBatchesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<MissionStatsBatch, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task HandleMissionEndedAsync_WhenNoBatches_ShouldNotMerge()
    {
        var session = new MissionSession { SessionId = "session-123" };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([]);

        await _subject.HandleMissionEndedAsync("session-123", 300, DateTime.UtcNow);

        _mockBatchesContext.Verify(x => x.Add(It.IsAny<MissionStatsBatch>()), Times.Never);
        _mockBatchesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<MissionStatsBatch, bool>>>()), Times.Never);
    }

    #endregion

    #region HandleMissionEndedAsync — FPS Computation

    [Fact]
    public async Task HandleMissionEndedAsync_ShouldComputeFpsStats()
    {
        var sessionId = "session-123";
        var events = new List<BsonDocument>
        {
            new()
            {
                { "type", "fps" },
                { "uid", "player1" },
                { "value", 60 }
            },
            new()
            {
                { "type", "fps" },
                { "uid", "player1" },
                { "value", 30 }
            },
            new()
            {
                { "type", "fps" },
                { "uid", "player1" },
                { "value", 45 }
            },
            new() { { "type", "shot" }, { "uid", "player1" } }
        };
        var batch = new MissionStatsBatch
        {
            Id = "batch-1",
            MissionSessionId = sessionId,
            Events = events
        };

        var session = new MissionSession { SessionId = sessionId };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([batch]);

        // Capture the update to verify computed values
        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
                               .Callback<Expression<Func<PlayerMissionStats, bool>>,
                                   UpdateDefinition<PlayerMissionStats>>((_, update) => capturedUpdate = update)
                               .Returns(Task.CompletedTask);

        await _subject.HandleMissionEndedAsync(sessionId, 300, DateTime.UtcNow);

        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
        capturedUpdate.Should().NotBeNull();

        // Render the update to verify values: min=30, max=60, avg=45, P1=30
        var rendered = capturedUpdate.RenderUpdate();
        var setDoc = rendered["$set"].AsBsonDocument;
        setDoc.GetValue("FpsMin", setDoc.GetValue("fpsMin", BsonNull.Value)).ToInt32().Should().Be(30);
        setDoc.GetValue("FpsMax", setDoc.GetValue("fpsMax", BsonNull.Value)).ToInt32().Should().Be(60);
        setDoc.GetValue("FpsAverage", setDoc.GetValue("fpsAverage", BsonNull.Value)).ToDouble().Should().Be(45.0);
        setDoc.GetValue("FpsP1", setDoc.GetValue("fpsP1", BsonNull.Value)).ToInt32().Should().Be(30);
    }

    [Fact]
    public async Task HandleMissionEndedAsync_WhenSessionNotFound_ShouldDoNothing()
    {
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns((MissionSession)null);

        await _subject.HandleMissionEndedAsync("nonexistent", 300, DateTime.UtcNow);

        _mockSessionsContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionSession>>()), Times.Never);
        _mockBatchesContext.Verify(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>()), Times.Never);
    }

    #endregion

    #region UpdateMissionStatsAsync

    [Fact]
    public async Task UpdateMissionStatsAsync_WhenNoExisting_ShouldCreateNew()
    {
        var sessionId = "session-123";
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns((MissionStats)null);

        var updates = new MissionStats { EventCounts = new Dictionary<string, int> { ["shot"] = 50, ["hit"] = 20 } };

        await _subject.UpdateMissionStatsAsync(sessionId, updates);

        _mockMissionStatsContext.Verify(
            x => x.Add(It.Is<MissionStats>(s => s.MissionSessionId == sessionId && s.EventCounts["shot"] == 50 && s.EventCounts["hit"] == 20)),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateMissionStatsAsync_WhenExisting_ShouldUseAtomicIncrement()
    {
        var sessionId = "session-123";
        var existing = new MissionStats { MissionSessionId = sessionId, EventCounts = new Dictionary<string, int> { ["shot"] = 30, ["hit"] = 10 } };
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns(existing);

        var updates = new MissionStats { EventCounts = new Dictionary<string, int> { ["shot"] = 20, ["kill"] = 5 } };

        await _subject.UpdateMissionStatsAsync(sessionId, updates);

        _mockMissionStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<MissionStats, bool>>>(), It.IsAny<UpdateDefinition<MissionStats>>()),
            Times.Once
        );
        _mockMissionStatsContext.Verify(x => x.Replace(It.IsAny<MissionStats>()), Times.Never);
    }

    #endregion
}
