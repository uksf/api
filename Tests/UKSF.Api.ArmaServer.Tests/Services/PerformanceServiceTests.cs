using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
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

        _mockSessionsContext.Verify(x => x.Update(session.Id, It.IsAny<UpdateDefinition<MissionSession>>()), Times.Once);
    }

    [Fact]
    public async Task HandlePerformanceEvent_ShouldUpdateRollingPlayerStats()
    {
        var session = new MissionSession
        {
            SessionId = "session-1",
            MissionStarted = DateTime.UtcNow.AddMinutes(-5),
            PlayerPerformance = []
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns((PlayerMissionStats)null);

        var players = new List<PlayerPerformance> { new() { Uid = "player1", Fps = [40, 45, 50] } };

        await _subject.HandlePerformanceEventAsync("session-1", [50], [], players);

        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
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

        _mockSessionsContext.Verify(x => x.Update(session.Id, It.IsAny<UpdateDefinition<MissionSession>>()), Times.Once);
    }

    [Fact]
    public async Task ComputeFinalFpsStats_ShouldComputeP1FromRleData()
    {
        var fpsSamples = Enumerable.Range(1, 100).Select(i => i).ToList();
        var session = new MissionSession { SessionId = "session-1", PlayerPerformance = [new PlayerPerformance { Uid = "player1", Fps = fpsSamples }] };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        await _subject.ComputeFinalFpsStatsAsync("session-1");

        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
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

        await _subject.ComputeFinalFpsStatsAsync("session-1");

        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
    }
}
