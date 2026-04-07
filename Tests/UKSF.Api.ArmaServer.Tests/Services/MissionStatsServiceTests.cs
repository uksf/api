using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class MissionStatsServiceTests
{
    static MissionStatsServiceTests()
    {
        // Register the camelCase convention used by the API at startup so rendered
        // update paths match production shape (e.g. killsByTargetType.infantry.count).
        ConventionRegistry.Register("TestCamelCase", new ConventionPack { new CamelCaseElementNameConvention() }, _ => true);
    }

    private readonly Mock<IMissionSessionsContext> _mockSessionsContext = new();
    private readonly Mock<IMissionStatsBatchesContext> _mockBatchesContext = new();
    private readonly Mock<IPlayerMissionStatsContext> _mockPlayerStatsContext = new();
    private readonly Mock<IMissionStatsContext> _mockMissionStatsContext = new();
    private readonly Mock<IPerformanceService> _mockPerformanceService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();

    private readonly MissionStatsService _subject;

    public MissionStatsServiceTests()
    {
        _subject = new MissionStatsService(
            _mockSessionsContext.Object,
            _mockBatchesContext.Object,
            _mockPlayerStatsContext.Object,
            _mockMissionStatsContext.Object,
            _mockPerformanceService.Object,
            _mockLogger.Object
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
                    AmmoBreakdown = new Dictionary<string, AmmoStats>
                        {
                            ["rhs_ammo_556x45_M855A1"] = new() { Shots = 3 }, ["rhs_ammo_556x45_M856"] = new() { Shots = 2 }
                        }
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
                    AmmoBreakdown = new Dictionary<string, AmmoStats>
                        {
                            ["rhs_ammo_556x45_M855A1"] = new() { Shots = 1 }, ["rhs_ammo_556x45_M862"] = new() { Shots = 2 }
                        }
                },
                ["rhs_weap_m249"] = new()
                {
                    Shots = 10,
                    Hits = 4,
                    AmmoBreakdown = new Dictionary<string, AmmoStats> { ["rhs_ammo_762x51_M80"] = new() { Shots = 10 } }
                }
            }
        };

        await _subject.UpdatePlayerStatsAsync(sessionId, playerUid, updates);

        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdatePlayerStatsAsync_WithKillsByTargetType_ShouldRenderNestedIncrementPaths()
    {
        var sessionId = "session-123";
        var playerUid = "76561198012345678";
        var existing = new PlayerMissionStats { MissionSessionId = sessionId, PlayerUid = playerUid };
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns(existing);

        var updates = new PlayerMissionStats
        {
            KillsByTargetType = new Dictionary<string, KillTargetTypeStats>
            {
                ["infantry"] = new() { Count = 3, Types = new Dictionary<string, int> { ["O_Soldier_F"] = 2, ["O_Officer_F"] = 1 } },
                ["vehicle"] = new() { Count = 1, Types = new Dictionary<string, int> { ["O_MRAP_02_F"] = 1 } }
            }
        };

        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
                               .Callback<Expression<Func<PlayerMissionStats, bool>>, UpdateDefinition<PlayerMissionStats>>((_, u) => capturedUpdate = u)
                               .Returns(Task.CompletedTask);

        await _subject.UpdatePlayerStatsAsync(sessionId, playerUid, updates);

        capturedUpdate.Should().NotBeNull();

        var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry.GetSerializer<PlayerMissionStats>();
        var rendered = capturedUpdate.Render(
            new MongoDB.Driver.RenderArgs<PlayerMissionStats>(serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)
        );
        var incDoc = rendered["$inc"].AsBsonDocument;

        incDoc.Contains("killsByTargetType.infantry.count").Should().BeTrue();
        incDoc["killsByTargetType.infantry.count"].AsInt32.Should().Be(3);

        incDoc.Contains("killsByTargetType.infantry.types.O_Soldier_F").Should().BeTrue();
        incDoc["killsByTargetType.infantry.types.O_Soldier_F"].AsInt32.Should().Be(2);

        incDoc.Contains("killsByTargetType.infantry.types.O_Officer_F").Should().BeTrue();
        incDoc["killsByTargetType.infantry.types.O_Officer_F"].AsInt32.Should().Be(1);

        incDoc.Contains("killsByTargetType.vehicle.count").Should().BeTrue();
        incDoc["killsByTargetType.vehicle.count"].AsInt32.Should().Be(1);

        incDoc.Contains("killsByTargetType.vehicle.types.O_MRAP_02_F").Should().BeTrue();
        incDoc["killsByTargetType.vehicle.types.O_MRAP_02_F"].AsInt32.Should().Be(1);
    }

    [Fact]
    public async Task UpdatePlayerStatsAsync_WithKillsByWeapon_ShouldRenderNestedIncrementPaths()
    {
        var sessionId = "session-123";
        var playerUid = "76561198012345678";
        var existing = new PlayerMissionStats { MissionSessionId = sessionId, PlayerUid = playerUid };
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns(existing);

        var updates = new PlayerMissionStats
        {
            KillsByWeapon = new Dictionary<string, KillWeaponStats>
            {
                ["rhs_weap_m4a1"] = new()
                {
                    Count = 3, Ammo = new Dictionary<string, int> { ["rhs_ammo_556x45_M855A1"] = 2, ["rhs_ammo_556x45_M856"] = 1 }
                }
            }
        };

        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
                               .Callback<Expression<Func<PlayerMissionStats, bool>>, UpdateDefinition<PlayerMissionStats>>((_, u) => capturedUpdate = u)
                               .Returns(Task.CompletedTask);

        await _subject.UpdatePlayerStatsAsync(sessionId, playerUid, updates);

        var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry.GetSerializer<PlayerMissionStats>();
        var rendered = capturedUpdate.Render(
            new MongoDB.Driver.RenderArgs<PlayerMissionStats>(serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)
        );
        var incDoc = rendered["$inc"].AsBsonDocument;

        incDoc["killsByWeapon.rhs_weap_m4a1.count"].AsInt32.Should().Be(3);
        incDoc["killsByWeapon.rhs_weap_m4a1.ammo.rhs_ammo_556x45_M855A1"].AsInt32.Should().Be(2);
        incDoc["killsByWeapon.rhs_weap_m4a1.ammo.rhs_ammo_556x45_M856"].AsInt32.Should().Be(1);
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
    public async Task HandleMissionEndedAsync_ShouldCallComputeFinalFpsStats()
    {
        var sessionId = "session-123";
        var session = new MissionSession { SessionId = sessionId };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([]);

        await _subject.HandleMissionEndedAsync(sessionId, 300, DateTime.UtcNow);

        _mockPerformanceService.Verify(x => x.ComputeFinalFpsStatsAsync(sessionId), Times.Once);
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

    [Fact]
    public async Task FinaliseKilledSessionAsync_WhenSessionNotFound_ShouldDoNothing()
    {
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns((MissionSession)null);

        await _subject.FinaliseKilledSessionAsync("nonexistent");

        _mockSessionsContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionSession>>()), Times.Never);
        _mockBatchesContext.Verify(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>()), Times.Never);
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_WhenAlreadyEnded_ShouldDoNothing()
    {
        var session = new MissionSession { SessionId = "session-123", MissionEnded = new DateTime(2025, 6, 14, 21, 0, 0) };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockSessionsContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionSession>>()), Times.Never);
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_ShouldSetMissionEndedFromLastBatchReceived()
    {
        var lastBatch = new DateTime(2025, 6, 14, 20, 30, 0);
        var missionStarted = new DateTime(2025, 6, 14, 20, 0, 0);
        var session = new MissionSession
        {
            Id = "id-1",
            SessionId = "session-123",
            Mission = "co40_op_eagle",
            Map = "Altis",
            MissionStarted = missionStarted,
            LastBatchReceived = lastBatch,
            PlayerPresence = []
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockSessionsContext.Setup(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                            .Callback(() => session.MissionEnded = lastBatch);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([]);
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns((MissionStats)null);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockSessionsContext.Verify(
            x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()),
            Times.Once
        );
        _mockPerformanceService.Verify(x => x.ComputeFinalFpsStatsAsync("session-123"), Times.Once);
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_ShouldCloseOpenPlayerPresenceEntries()
    {
        var lastBatch = new DateTime(2025, 6, 14, 20, 30, 0);
        var session = new MissionSession
        {
            Id = "id-1",
            SessionId = "session-123",
            Mission = "co40_op_eagle",
            Map = "Altis",
            MissionStarted = new DateTime(2025, 6, 14, 20, 0, 0),
            LastBatchReceived = lastBatch,
            PlayerPresence =
            [
                new PlayerPresence
                {
                    Uid = "uid-1",
                    Name = "Player1",
                    Connected = new DateTime(2025, 6, 14, 20, 0, 0),
                    Disconnected = new DateTime(2025, 6, 14, 20, 10, 0)
                },
                new PlayerPresence
                {
                    Uid = "uid-2",
                    Name = "Player2",
                    Connected = new DateTime(2025, 6, 14, 20, 5, 0),
                    Disconnected = null
                },
                new PlayerPresence
                {
                    Uid = "uid-3",
                    Name = "Player3",
                    Connected = new DateTime(2025, 6, 14, 20, 8, 0),
                    Disconnected = null
                }
            ]
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockSessionsContext.Setup(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                            .Callback(() => session.MissionEnded = lastBatch);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([]);
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns((MissionStats)null);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockSessionsContext.Verify(
            x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()),
            Times.Once
        );
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_ShouldBackfillSyntheticEvents()
    {
        var lastBatch = new DateTime(2025, 6, 14, 20, 30, 0);
        var session = new MissionSession
        {
            Id = "id-1",
            SessionId = "session-123",
            Mission = "co40_op_eagle",
            Map = "Altis",
            MissionStarted = new DateTime(2025, 6, 14, 20, 0, 0),
            LastBatchReceived = lastBatch,
            PlayerPresence =
            [
                new PlayerPresence
                {
                    Uid = "uid-1",
                    Name = "Player1",
                    Connected = new DateTime(2025, 6, 14, 20, 0, 0),
                    Disconnected = null
                },
                new PlayerPresence
                {
                    Uid = "uid-2",
                    Name = "Player2",
                    Connected = new DateTime(2025, 6, 14, 20, 5, 0),
                    Disconnected = null
                }
            ]
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockSessionsContext.Setup(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                            .Callback(() => session.MissionEnded = lastBatch);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([]);
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns((MissionStats)null);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockBatchesContext.Verify(
            x => x.Add(
                It.Is<MissionStatsBatch>(b => b.MissionSessionId == "session-123" && b.Events.Count == 3 && b.Mission == "co40_op_eagle" && b.Map == "Altis")
            ),
            Times.Once
        );

        _mockMissionStatsContext.Verify(
            x => x.Add(
                It.Is<MissionStats>(s => s.MissionSessionId == "session-123" && s.EventCounts["mission_ended"] == 1 && s.EventCounts["player_disconnected"] == 2
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_WhenNoOpenPresence_ShouldStillBackfillMissionEndedEvent()
    {
        var lastBatch = new DateTime(2025, 6, 14, 20, 30, 0);
        var session = new MissionSession
        {
            Id = "id-1",
            SessionId = "session-123",
            Mission = "co40_op_eagle",
            Map = "Altis",
            MissionStarted = new DateTime(2025, 6, 14, 20, 0, 0),
            LastBatchReceived = lastBatch,
            PlayerPresence = []
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockSessionsContext.Setup(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                            .Callback(() => session.MissionEnded = lastBatch);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([]);
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns((MissionStats)null);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockBatchesContext.Verify(x => x.Add(It.Is<MissionStatsBatch>(b => b.Events.Count == 1)), Times.Once);

        _mockMissionStatsContext.Verify(
            x => x.Add(It.Is<MissionStats>(s => s.EventCounts["mission_ended"] == 1 && !s.EventCounts.ContainsKey("player_disconnected"))),
            Times.Once
        );
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_ShouldCallMergeAndComputeFps()
    {
        var lastBatch = new DateTime(2025, 6, 14, 20, 30, 0);
        var session = new MissionSession
        {
            Id = "id-1",
            SessionId = "session-123",
            Mission = "co40_op_eagle",
            Map = "Altis",
            MissionStarted = new DateTime(2025, 6, 14, 20, 0, 0),
            LastBatchReceived = lastBatch,
            PlayerPresence = []
        };
        var batch1 = new MissionStatsBatch
        {
            Id = "b1",
            MissionSessionId = "session-123",
            Mission = "co40_op_eagle",
            Map = "Altis",
            ReceivedAt = new DateTime(2025, 6, 14, 20, 0, 0),
            Events = [new BsonDocument { { "type", "shot" } }]
        };
        var batch2 = new MissionStatsBatch
        {
            Id = "b2",
            MissionSessionId = "session-123",
            Mission = "co40_op_eagle",
            Map = "Altis",
            ReceivedAt = new DateTime(2025, 6, 14, 20, 1, 0),
            Events = [new BsonDocument { { "type", "hit" } }]
        };

        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockSessionsContext.Setup(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                            .Callback(() => session.MissionEnded = lastBatch);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([batch1, batch2]);
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns((MissionStats)null);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockBatchesContext.Verify(x => x.Add(It.IsAny<MissionStatsBatch>()), Times.Exactly(2));
        _mockBatchesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<MissionStatsBatch, bool>>>()), Times.Once);
        _mockPerformanceService.Verify(x => x.ComputeFinalFpsStatsAsync("session-123"), Times.Once);
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_WhenMissionStatsAlreadyExist_ShouldIncrementEventCounts()
    {
        var lastBatch = new DateTime(2025, 6, 14, 20, 30, 0);
        var session = new MissionSession
        {
            Id = "id-1",
            SessionId = "session-123",
            Mission = "co40_op_eagle",
            Map = "Altis",
            MissionStarted = new DateTime(2025, 6, 14, 20, 0, 0),
            LastBatchReceived = lastBatch,
            PlayerPresence =
            [
                new PlayerPresence
                {
                    Uid = "uid-1",
                    Name = "Player1",
                    Connected = new DateTime(2025, 6, 14, 20, 0, 0),
                    Disconnected = null
                }
            ]
        };
        var existingStats = new MissionStats { MissionSessionId = "session-123", EventCounts = new Dictionary<string, int> { ["shot"] = 50, ["hit"] = 20 } };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockSessionsContext.Setup(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                            .Callback(() => session.MissionEnded = lastBatch);
        _mockBatchesContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsBatch, bool>>())).Returns([]);
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns(existingStats);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockMissionStatsContext.Verify(x => x.Add(It.IsAny<MissionStats>()), Times.Never);
        _mockMissionStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<MissionStats, bool>>>(), It.IsAny<UpdateDefinition<MissionStats>>()),
            Times.Once
        );
    }
}
