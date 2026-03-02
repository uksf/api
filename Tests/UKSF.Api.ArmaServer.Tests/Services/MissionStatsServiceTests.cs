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
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class MissionStatsServiceTests
{
    private readonly Mock<IMissionSessionsContext> _mockSessionsContext = new();
    private readonly Mock<IMissionStatsBatchesContext> _mockBatchesContext = new();
    private readonly Mock<IPlayerMissionStatsContext> _mockPlayerStatsContext = new();
    private readonly Mock<IMissionStatsContext> _mockMissionStatsContext = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();

    private readonly MissionStatsService _subject;

    public MissionStatsServiceTests()
    {
        _subject = new MissionStatsService(
            _mockSessionsContext.Object,
            _mockBatchesContext.Object,
            _mockPlayerStatsContext.Object,
            _mockMissionStatsContext.Object,
            _mockVariablesService.Object
        );
    }

    #region FindOrCreateSessionAsync

    [Fact]
    public async Task FindOrCreateSessionAsync_WhenNoMatchingSession_ShouldCreateNewSession()
    {
        var receivedAt = new DateTime(2025, 6, 14, 20, 0, 0); // Saturday
        _mockVariablesService.Setup(x => x.GetVariable("MISSION_STATS_SESSION_GAP_HOURS")).Returns(new DomainVariableItem { Item = "4" });
        _mockSessionsContext.Setup(x => x.Get(It.IsAny<Func<MissionSession, bool>>())).Returns([]);

        var result = await _subject.FindOrCreateSessionAsync("co40_op_eagle", "Altis", receivedAt);

        result.Mission.Should().Be("co40_op_eagle");
        result.Map.Should().Be("Altis");
        result.FirstBatchReceived.Should().Be(receivedAt);
        result.LastBatchReceived.Should().Be(receivedAt);
        result.TotalBatchesReceived.Should().Be(1);
        _mockSessionsContext.Verify(x => x.Add(It.IsAny<MissionSession>()), Times.Once);
    }

    [Fact]
    public async Task FindOrCreateSessionAsync_WhenMatchingSessionWithinGap_ShouldReturnExistingAndUpdateAtomically()
    {
        var now = new DateTime(2025, 6, 14, 20, 0, 0);
        var existingSession = new MissionSession
        {
            Mission = "co40_op_eagle",
            Map = "Altis",
            LastBatchReceived = now.AddHours(-1),
            TotalBatchesReceived = 3
        };
        _mockVariablesService.Setup(x => x.GetVariable("MISSION_STATS_SESSION_GAP_HOURS")).Returns(new DomainVariableItem { Item = "4" });
        _mockSessionsContext.Setup(x => x.Get(It.IsAny<Func<MissionSession, bool>>())).Returns([existingSession]);

        var result = await _subject.FindOrCreateSessionAsync("co40_op_eagle", "Altis", now);

        result.Id.Should().Be(existingSession.Id);
        result.TotalBatchesReceived.Should().Be(4);
        result.LastBatchReceived.Should().Be(now);
        _mockSessionsContext.Verify(x => x.Add(It.IsAny<MissionSession>()), Times.Never);
        _mockSessionsContext.Verify(x => x.Update(existingSession.Id, It.IsAny<UpdateDefinition<MissionSession>>()), Times.Once);
    }

    [Fact]
    public async Task FindOrCreateSessionAsync_WhenMatchingSessionBeyondGap_ShouldCreateNewSession()
    {
        var now = new DateTime(2025, 6, 14, 20, 0, 0);
        _mockVariablesService.Setup(x => x.GetVariable("MISSION_STATS_SESSION_GAP_HOURS")).Returns(new DomainVariableItem { Item = "4" });
        _mockSessionsContext.Setup(x => x.Get(It.IsAny<Func<MissionSession, bool>>())).Returns([]);

        var result = await _subject.FindOrCreateSessionAsync("co40_op_eagle", "Altis", now);

        _mockSessionsContext.Verify(x => x.Add(It.IsAny<MissionSession>()), Times.Once);
        result.TotalBatchesReceived.Should().Be(1);
    }

    [Theory]
    [InlineData(DayOfWeek.Saturday, MissionType.MainOp)]
    [InlineData(DayOfWeek.Wednesday, MissionType.Training)]
    [InlineData(DayOfWeek.Monday, MissionType.SideOp)]
    [InlineData(DayOfWeek.Thursday, MissionType.SideOp)]
    [InlineData(DayOfWeek.Sunday, MissionType.SideOp)]
    [InlineData(DayOfWeek.Tuesday, MissionType.SideOp)]
    [InlineData(DayOfWeek.Friday, MissionType.SideOp)]
    public async Task FindOrCreateSessionAsync_ShouldSetCorrectMissionTypeFromDayOfWeek(DayOfWeek dayOfWeek, MissionType expectedType)
    {
        var baseDate = new DateTime(2025, 6, 9); // Monday
        var daysToAdd = ((int)dayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var receivedAt = baseDate.AddDays(daysToAdd).AddHours(20);

        _mockVariablesService.Setup(x => x.GetVariable("MISSION_STATS_SESSION_GAP_HOURS")).Returns(new DomainVariableItem { Item = "4" });
        _mockSessionsContext.Setup(x => x.Get(It.IsAny<Func<MissionSession, bool>>())).Returns([]);

        var result = await _subject.FindOrCreateSessionAsync("test_mission", "Altis", receivedAt);

        result.Type.Should().Be(expectedType);
    }

    [Fact]
    public async Task FindOrCreateSessionAsync_WhenVariableItemNotNumeric_ShouldDefaultTo4Hours()
    {
        var now = new DateTime(2025, 6, 14, 20, 0, 0);
        _mockVariablesService.Setup(x => x.GetVariable("MISSION_STATS_SESSION_GAP_HOURS")).Returns(new DomainVariableItem { Item = "not_a_number" });
        _mockSessionsContext.Setup(x => x.Get(It.IsAny<Func<MissionSession, bool>>())).Returns([]);

        var result = await _subject.FindOrCreateSessionAsync("test_mission", "Altis", now);

        result.Should().NotBeNull();
        _mockSessionsContext.Verify(x => x.Add(It.IsAny<MissionSession>()), Times.Once);
    }

    [Fact]
    public async Task FindOrCreateSessionAsync_WhenVariableItemIsNull_ShouldDefaultTo4Hours()
    {
        var now = new DateTime(2025, 6, 14, 20, 0, 0);
        _mockVariablesService.Setup(x => x.GetVariable("MISSION_STATS_SESSION_GAP_HOURS")).Returns(new DomainVariableItem { Item = null });
        _mockSessionsContext.Setup(x => x.Get(It.IsAny<Func<MissionSession, bool>>())).Returns([]);

        var result = await _subject.FindOrCreateSessionAsync("test_mission", "Altis", now);

        result.Should().NotBeNull();
        _mockSessionsContext.Verify(x => x.Add(It.IsAny<MissionSession>()), Times.Once);
    }

    [Fact]
    public async Task FindOrCreateSessionAsync_WhenMultipleSessionsMatch_ShouldReturnMostRecent()
    {
        var now = new DateTime(2025, 6, 14, 20, 0, 0);
        var olderSession = new MissionSession
        {
            Mission = "co40_op_eagle",
            Map = "Altis",
            LastBatchReceived = now.AddHours(-3),
            TotalBatchesReceived = 2
        };
        var newerSession = new MissionSession
        {
            Mission = "co40_op_eagle",
            Map = "Altis",
            LastBatchReceived = now.AddHours(-1),
            TotalBatchesReceived = 5
        };
        _mockVariablesService.Setup(x => x.GetVariable("MISSION_STATS_SESSION_GAP_HOURS")).Returns(new DomainVariableItem { Item = "4" });
        _mockSessionsContext.Setup(x => x.Get(It.IsAny<Func<MissionSession, bool>>())).Returns([olderSession, newerSession]);

        var result = await _subject.FindOrCreateSessionAsync("co40_op_eagle", "Altis", now);

        result.Id.Should().Be(newerSession.Id);
    }

    #endregion

    #region StoreRawBatchAsync

    [Fact]
    public async Task StoreRawBatchAsync_ShouldStoreBatchWithSessionReference()
    {
        var sessionId = ObjectId.GenerateNewId().ToString();
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
        var sessionId = ObjectId.GenerateNewId().ToString();
        var playerUid = "76561198012345678";
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns((PlayerMissionStats)null);

        var updates = new PlayerMissionStats
        {
            TotalShots = 10,
            TotalHits = 5,
            TotalDistance = 1000.5
        };

        await _subject.UpdatePlayerStatsAsync(sessionId, playerUid, updates);

        _mockPlayerStatsContext.Verify(
            x => x.Add(
                It.Is<PlayerMissionStats>(s => s.MissionSessionId == sessionId &&
                                               s.PlayerUid == playerUid &&
                                               s.TotalShots == 10 &&
                                               s.TotalHits == 5 &&
                                               Math.Abs(s.TotalDistance - 1000.5) < 0.01
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdatePlayerStatsAsync_WhenExistingStats_ShouldUseAtomicIncrement()
    {
        var sessionId = ObjectId.GenerateNewId().ToString();
        var playerUid = "76561198012345678";
        var existing = new PlayerMissionStats
        {
            MissionSessionId = sessionId,
            PlayerUid = playerUid,
            TotalShots = 10,
            TotalHits = 5,
            TotalDistance = 500
        };
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns(existing);

        var updates = new PlayerMissionStats
        {
            TotalShots = 8,
            TotalHits = 3,
            TotalDistance = 300.5
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
        var sessionId = ObjectId.GenerateNewId().ToString();
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

    [Fact]
    public async Task UpdatePlayerStatsAsync_WhenExistingStats_WithBodyPartHits_ShouldUseAtomicUpdate()
    {
        var sessionId = ObjectId.GenerateNewId().ToString();
        var playerUid = "76561198012345678";
        var existing = new PlayerMissionStats
        {
            MissionSessionId = sessionId,
            PlayerUid = playerUid,
            BodyPartHits = new Dictionary<string, int> { ["head"] = 2, ["body"] = 5 }
        };
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns(existing);

        var updates = new PlayerMissionStats { BodyPartHits = new Dictionary<string, int> { ["body"] = 3, ["legs"] = 1 } };

        await _subject.UpdatePlayerStatsAsync(sessionId, playerUid, updates);

        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
    }

    #endregion

    #region UpdateMissionStatsAsync

    [Fact]
    public async Task UpdateMissionStatsAsync_WhenNoExisting_ShouldCreateNew()
    {
        var sessionId = ObjectId.GenerateNewId().ToString();
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
        var sessionId = ObjectId.GenerateNewId().ToString();
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
