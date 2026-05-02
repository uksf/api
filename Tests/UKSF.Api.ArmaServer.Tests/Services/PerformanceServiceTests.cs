using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class PerformanceServiceTests
{
    private readonly Mock<IMissionSessionsContext> _mockSessionsContext = new();
    private readonly Mock<IPlayerMissionStatsContext> _mockPlayerStatsContext = new();
    private readonly PerformanceService _subject;

    public PerformanceServiceTests()
    {
        _subject = new PerformanceService(_mockSessionsContext.Object, _mockPlayerStatsContext.Object);
    }

    [Fact]
    public async Task HandlePerformanceEvent_ShouldAppendServerFpsSamples()
    {
        var session = new MissionSession { SessionId = "session-1", MissionStarted = DateTime.UtcNow.AddMinutes(-5) };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        await _subject.HandlePerformanceEventAsync("session-1", [48, 49, 47], [], []);

        _mockSessionsContext.Verify(x => x.Update(session.Id, It.IsAny<UpdateDefinition<MissionSession>>()), Times.Once);
    }

    [Fact]
    public async Task HandlePerformanceEvent_WhenSessionNotFound_ShouldDoNothing()
    {
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns((MissionSession)null);

        await _subject.HandlePerformanceEventAsync("nonexistent", [48], [], []);

        _mockSessionsContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionSession>>()), Times.Never);
    }

    [Fact]
    public async Task HandlePerformanceEvent_ShouldAppendHeadlessClientSamples()
    {
        var session = new MissionSession
        {
            SessionId = "session-1",
            MissionStarted = DateTime.UtcNow.AddMinutes(-5),
            HeadlessClientPerformance = [new HeadlessClientPerformance { Name = "HC1", Fps = [45, 46] }]
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        var headlessClients = new List<HeadlessClientPerformance> { new() { Name = "HC1", Fps = [47, 48] } };

        await _subject.HandlePerformanceEventAsync("session-1", [50], headlessClients, []);

        _mockSessionsContext.Verify(x => x.Update(session.Id, It.IsAny<UpdateDefinition<MissionSession>>()), Times.Once);
    }

    [Fact]
    public async Task HandlePerformanceEvent_ShouldCreateNewHeadlessClientEntry()
    {
        var session = new MissionSession
        {
            SessionId = "session-1",
            MissionStarted = DateTime.UtcNow.AddMinutes(-5),
            HeadlessClientPerformance = []
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        var headlessClients = new List<HeadlessClientPerformance> { new() { Name = "HC1", Fps = [49, 50] } };

        await _subject.HandlePerformanceEventAsync("session-1", [50], headlessClients, []);

        // Two writes: phase 1 pushes the new HC, phase 2 appends serverFps (no per-element appends, since the HC was just added).
        _mockSessionsContext.Verify(x => x.Update(session.Id, It.IsAny<UpdateDefinition<MissionSession>>()), Times.Exactly(2));
    }

    // Regression: when an event contains a NEW player and an EXISTING player together,
    // the new player's Fps must arrive only via the row pushed in phase 1, not also via
    // a phase-2 positional append (which would double the values). Renders both update
    // documents to BSON and asserts phase 2 only touches the existing player's index.
    [Fact]
    public async Task HandlePerformanceEvent_NewAndExistingPlayer_ShouldNotDoubleAppendNew()
    {
        var session = new MissionSession
        {
            SessionId = "session-1",
            MissionStarted = DateTime.UtcNow.AddMinutes(-5),
            PlayerPerformance = [new PlayerPerformance { Uid = "EXISTING", Fps = [60, 61] }]
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        var capturedUpdates = new List<BsonValue>();
        var renderArgs = new RenderArgs<MissionSession>(BsonSerializer.SerializerRegistry.GetSerializer<MissionSession>(), BsonSerializer.SerializerRegistry);
        _mockSessionsContext.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                            .Callback<string, UpdateDefinition<MissionSession>>((_, def) => capturedUpdates.Add(def.Render(renderArgs)))
                            .Returns(Task.CompletedTask);

        var players = new List<PlayerPerformance> { new() { Uid = "NEW", Fps = [70, 71] }, new() { Uid = "EXISTING", Fps = [62, 63] } };

        await _subject.HandlePerformanceEventAsync("session-1", [], [], players);

        capturedUpdates.Should().HaveCount(2);

        // Phase 2 must reference index 0 (the EXISTING player), not index 1 (where NEW
        // would land after phase 1). If FindIndex picked up the just-pushed NEW row, this
        // would assert against playerPerformance.1.fps too — that's the double-append bug.
        var phaseTwoJson = capturedUpdates[1].ToJson();
        phaseTwoJson.Should().Contain("playerPerformance.0.fps");
        phaseTwoJson.Should().NotContain("playerPerformance.1.fps");
    }

    [Fact]
    public async Task HandlePerformanceEvent_ShouldExtendGapForAbsentPlayer()
    {
        var session = new MissionSession
        {
            SessionId = "session-1",
            MissionStarted = DateTime.UtcNow.AddMinutes(-5),
            PlayerPerformance = [new PlayerPerformance { Uid = "player1", Fps = [40, 45] }]
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        await _subject.HandlePerformanceEventAsync("session-1", [50], [], []);

        // Two writes: pushes (serverFps) + gap-extensions (player1 absent). Splitting these
        // is what prevents the Code 40 path conflict between $push on `.fps` and $set on
        // `.fps.<n>` for the same player index.
        _mockSessionsContext.Verify(x => x.Update(session.Id, It.IsAny<UpdateDefinition<MissionSession>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ComputeFinalFpsStats_ShouldComputeP1AndAverageFromData()
    {
        var fpsSamples = Enumerable.Range(1, 100).Select(i => i).ToList();
        var session = new MissionSession { SessionId = "session-1", PlayerPerformance = [new PlayerPerformance { Uid = "player1", Fps = fpsSamples }] };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        var expectedUpdate = Builders<PlayerMissionStats>.Update.SetOnInsert(x => x.MissionSessionId, "session-1")
                                                         .SetOnInsert(x => x.PlayerUid, "player1")
                                                         .Set(x => x.FpsP1, 1)
                                                         .Set(x => x.FpsAverage, 50.5)
                                                         .Min(x => x.FpsMin, 1)
                                                         .Max(x => x.FpsMax, 100)
                                                         .RenderUpdate();

        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
                               .Callback((Expression<Func<PlayerMissionStats, bool>> _, UpdateDefinition<PlayerMissionStats> update) => capturedUpdate = update)
                               .Returns(Task.CompletedTask);

        await _subject.ComputeFinalFpsStatsAsync("session-1");

        capturedUpdate.Should().NotBeNull();
        capturedUpdate.RenderUpdate().Should().BeEquivalentTo(expectedUpdate);
    }

    [Fact]
    public async Task ComputeFinalFpsStats_ShouldSkipGapValues()
    {
        var fps = new List<int>
        {
            40,
            45,
            -10,
            50,
            55
        };
        var session = new MissionSession { SessionId = "session-1", PlayerPerformance = [new PlayerPerformance { Uid = "player1", Fps = fps }] };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        var expectedUpdate = Builders<PlayerMissionStats>.Update.SetOnInsert(x => x.MissionSessionId, "session-1")
                                                         .SetOnInsert(x => x.PlayerUid, "player1")
                                                         .Set(x => x.FpsP1, 40)
                                                         .Set(x => x.FpsAverage, 47.5)
                                                         .Min(x => x.FpsMin, 40)
                                                         .Max(x => x.FpsMax, 55)
                                                         .RenderUpdate();

        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
                               .Callback((Expression<Func<PlayerMissionStats, bool>> _, UpdateDefinition<PlayerMissionStats> update) => capturedUpdate = update)
                               .Returns(Task.CompletedTask);

        await _subject.ComputeFinalFpsStatsAsync("session-1");

        capturedUpdate.Should().NotBeNull();
        capturedUpdate.RenderUpdate().Should().BeEquivalentTo(expectedUpdate);
    }

    [Fact]
    public async Task ComputeFinalFpsStatsAsync_ComputesAverageFromSessionFpsSamples_NotFromRollingTotals()
    {
        var fps = new List<int>
        {
            40,
            50,
            60
        };
        var session = new MissionSession { SessionId = "session-1", PlayerPerformance = [new PlayerPerformance { Uid = "player1", Fps = fps }] };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        var expectedUpdate = Builders<PlayerMissionStats>.Update.SetOnInsert(x => x.MissionSessionId, "session-1")
                                                         .SetOnInsert(x => x.PlayerUid, "player1")
                                                         .Set(x => x.FpsP1, 40)
                                                         .Set(x => x.FpsAverage, 50.0)
                                                         .Min(x => x.FpsMin, 40)
                                                         .Max(x => x.FpsMax, 60)
                                                         .RenderUpdate();

        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
                               .Callback((Expression<Func<PlayerMissionStats, bool>> _, UpdateDefinition<PlayerMissionStats> update) => capturedUpdate = update)
                               .Returns(Task.CompletedTask);

        await _subject.ComputeFinalFpsStatsAsync("session-1");

        capturedUpdate.Should().NotBeNull();
        capturedUpdate.RenderUpdate().Should().BeEquivalentTo(expectedUpdate);
    }

    [Fact]
    public async Task ComputeFinalFpsStatsAsync_UsesUpsert_NotCheckThenInsert()
    {
        var fps = new List<int>
        {
            40,
            45,
            50,
            55
        };
        var session = new MissionSession { SessionId = "session-1", PlayerPerformance = [new PlayerPerformance { Uid = "player1", Fps = fps }] };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        await _subject.ComputeFinalFpsStatsAsync("session-1");

        _mockPlayerStatsContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
        _mockPlayerStatsContext.Verify(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>()), Times.Never);
        _mockPlayerStatsContext.Verify(x => x.Add(It.IsAny<PlayerMissionStats>()), Times.Never);
        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Never
        );
    }
}
