using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Mock<IRawEventStore> _mockRawEventStore = new();
    private readonly Mock<IPlayerMissionStatsContext> _mockPlayerStatsContext = new();
    private readonly Mock<IMissionStatsContext> _mockMissionStatsContext = new();
    private readonly Mock<IPerformanceService> _mockPerformanceService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();

    private readonly MissionStatsService _subject;

    public MissionStatsServiceTests()
    {
        _subject = new MissionStatsService(
            _mockSessionsContext.Object,
            _mockRawEventStore.Object,
            _mockPlayerStatsContext.Object,
            _mockMissionStatsContext.Object,
            _mockPerformanceService.Object,
            _mockLogger.Object
        );
    }

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

    [Fact]
    public async Task UpdatePlayerStatsAsync_ShouldUseAtomicUpsertWithSetOnInsertIdentifiers()
    {
        var sessionId = "session-123";
        var playerUid = "76561198012345678";

        var updates = new PlayerMissionStats
        {
            TotalShots = 10,
            TotalHits = 5,
            DistanceOnFoot = 1000.5
        };

        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
                               .Callback<Expression<Func<PlayerMissionStats, bool>>, UpdateDefinition<PlayerMissionStats>>((_, u) => capturedUpdate = u)
                               .Returns(Task.CompletedTask);

        await _subject.UpdatePlayerStatsAsync(sessionId, playerUid, updates);

        _mockPlayerStatsContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
        _mockPlayerStatsContext.Verify(x => x.Add(It.IsAny<PlayerMissionStats>()), Times.Never);
        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Never
        );

        capturedUpdate.Should().NotBeNull();
        var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry.GetSerializer<PlayerMissionStats>();
        var rendered = capturedUpdate.Render(
            new MongoDB.Driver.RenderArgs<PlayerMissionStats>(serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)
        );

        var setOnInsert = rendered["$setOnInsert"].AsBsonDocument;
        setOnInsert["missionSessionId"].AsString.Should().Be(sessionId);
        setOnInsert["playerUid"].AsString.Should().Be(playerUid);

        var incDoc = rendered["$inc"].AsBsonDocument;
        incDoc["totalShots"].AsInt32.Should().Be(10);
        incDoc["totalHits"].AsInt32.Should().Be(5);
        incDoc["distanceOnFoot"].AsDouble.Should().BeApproximately(1000.5, 0.01);
    }

    [Fact]
    public async Task UpdatePlayerStatsAsync_WithWeaponBreakdown_ShouldUseAtomicUpsert()
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
            x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
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
        _mockPlayerStatsContext.Setup(x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
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
        _mockPlayerStatsContext.Setup(x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
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

    [Fact]
    public async Task HandleMissionEndedAsync_ShouldCallComputeFinalFpsStats()
    {
        var sessionId = "session-123";
        var session = new MissionSession { SessionId = sessionId };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        await _subject.HandleMissionEndedAsync(sessionId, 300, DateTime.UtcNow);

        _mockPerformanceService.Verify(x => x.ComputeFinalFpsStatsAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task HandleMissionEndedAsync_WhenSessionNotFound_ShouldDoNothing()
    {
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns((MissionSession)null);

        await _subject.HandleMissionEndedAsync("nonexistent", 300, DateTime.UtcNow);

        _mockSessionsContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionSession>>()), Times.Never);
        _mockPerformanceService.Verify(x => x.ComputeFinalFpsStatsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateMissionStatsAsync_ShouldUseAtomicUpsertWithSetOnInsertSessionId()
    {
        var sessionId = "session-123";
        var updates = new MissionStats { VehiclesDestroyed = 3 };

        UpdateDefinition<MissionStats> capturedUpdate = null;
        _mockMissionStatsContext.Setup(x => x.Upsert(It.IsAny<Expression<Func<MissionStats, bool>>>(), It.IsAny<UpdateDefinition<MissionStats>>()))
                                .Callback<Expression<Func<MissionStats, bool>>, UpdateDefinition<MissionStats>>((_, u) => capturedUpdate = u)
                                .Returns(Task.CompletedTask);

        await _subject.UpdateMissionStatsAsync(sessionId, updates);

        _mockMissionStatsContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<MissionStats, bool>>>(), It.IsAny<UpdateDefinition<MissionStats>>()),
            Times.Once
        );
        _mockMissionStatsContext.Verify(x => x.Add(It.IsAny<MissionStats>()), Times.Never);
        _mockMissionStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<MissionStats, bool>>>(), It.IsAny<UpdateDefinition<MissionStats>>()),
            Times.Never
        );

        capturedUpdate.Should().NotBeNull();
        var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry.GetSerializer<MissionStats>();
        var rendered = capturedUpdate.Render(
            new MongoDB.Driver.RenderArgs<MissionStats>(serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)
        );

        rendered["$setOnInsert"].AsBsonDocument["missionSessionId"].AsString.Should().Be(sessionId);
        var incDoc = rendered["$inc"].AsBsonDocument;
        incDoc["vehiclesDestroyed"].AsInt32.Should().Be(3);
    }

    [Fact]
    public async Task UpdateMissionStatsAsync_WhenNoIncrements_ShouldBeNoOp()
    {
        await _subject.UpdateMissionStatsAsync("session-123", new MissionStats());

        _mockMissionStatsContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<MissionStats, bool>>>(), It.IsAny<UpdateDefinition<MissionStats>>()),
            Times.Never
        );
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_WhenSessionNotFound_ShouldDoNothing()
    {
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns((MissionSession)null);

        await _subject.FinaliseKilledSessionAsync("nonexistent");

        _mockSessionsContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionSession>>()), Times.Never);
        _mockRawEventStore.Verify(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<List<BsonDocument>>()), Times.Never);
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
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns((MissionStats)null);

        List<BsonDocument> capturedEvents = null;
        _mockRawEventStore.Setup(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<List<BsonDocument>>()))
                          .Callback<string, List<BsonDocument>>((_, e) => capturedEvents = e)
                          .Returns(Task.CompletedTask);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockRawEventStore.Verify(x => x.StoreAsync("session-123", It.IsAny<List<BsonDocument>>()), Times.Once);
        capturedEvents.Should().NotBeNull();
        capturedEvents.Should().HaveCount(3);
        capturedEvents[0].GetValue("type").AsString.Should().Be("mission_ended");
        capturedEvents.Skip(1).All(e => e.GetValue("type").AsString == "player_disconnected").Should().BeTrue();

        _mockMissionStatsContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<MissionStats, bool>>>(), It.IsAny<UpdateDefinition<MissionStats>>()),
            Times.Never
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
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns((MissionStats)null);

        List<BsonDocument> capturedEvents = null;
        _mockRawEventStore.Setup(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<List<BsonDocument>>()))
                          .Callback<string, List<BsonDocument>>((_, e) => capturedEvents = e)
                          .Returns(Task.CompletedTask);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockRawEventStore.Verify(x => x.StoreAsync("session-123", It.IsAny<List<BsonDocument>>()), Times.Once);
        capturedEvents.Should().NotBeNull();
        capturedEvents.Should().HaveCount(1);
        capturedEvents[0].GetValue("type").AsString.Should().Be("mission_ended");

        _mockMissionStatsContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<MissionStats, bool>>>(), It.IsAny<UpdateDefinition<MissionStats>>()),
            Times.Never
        );
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_ShouldComputeFps()
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
        _mockMissionStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionStats, bool>>())).Returns((MissionStats)null);

        await _subject.FinaliseKilledSessionAsync("session-123");

        _mockRawEventStore.Verify(x => x.StoreAsync("session-123", It.IsAny<List<BsonDocument>>()), Times.Once);
        _mockPerformanceService.Verify(x => x.ComputeFinalFpsStatsAsync("session-123"), Times.Once);
    }
}
