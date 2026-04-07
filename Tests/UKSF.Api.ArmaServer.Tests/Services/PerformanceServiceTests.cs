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
using UKSF.Api.Core;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class PerformanceServiceTests
{
    private readonly Mock<IMissionSessionsContext> _mockSessionsContext = new();
    private readonly Mock<IPlayerMissionStatsContext> _mockPlayerStatsContext = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly PerformanceService _subject;

    public PerformanceServiceTests()
    {
        _subject = new PerformanceService(_mockSessionsContext.Object, _mockPlayerStatsContext.Object, _mockLogger.Object);
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
    public async Task HandlePerformanceEvent_WhenNewPlayer_ShouldCreateStatsDocument()
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
            x => x.Add(
                It.Is<PlayerMissionStats>(s => s.MissionSessionId == "session-1" &&
                                               s.PlayerUid == "player1" &&
                                               s.FpsMin == 40 &&
                                               s.FpsMax == 50 &&
                                               s.FpsSampleCount == 3 &&
                                               Math.Abs(s.FpsSampleSum - 135) < 0.01
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandlePerformanceEvent_WhenExistingPlayer_ShouldUpdateRollingStats()
    {
        var session = new MissionSession
        {
            SessionId = "session-1",
            MissionStarted = DateTime.UtcNow.AddMinutes(-5),
            PlayerPerformance = [new PlayerPerformance { Uid = "player1", Fps = [40, 45] }]
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        var existingStats = new PlayerMissionStats
        {
            MissionSessionId = "session-1",
            PlayerUid = "player1",
            FpsMin = 38,
            FpsMax = 45,
            FpsSampleCount = 2,
            FpsSampleSum = 83
        };
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns(existingStats);

        var players = new List<PlayerPerformance> { new() { Uid = "player1", Fps = [50, 55] } };

        await _subject.HandlePerformanceEventAsync("session-1", [50], [], players);

        // Should use atomic Min/Max/Inc update, not Add
        _mockPlayerStatsContext.Verify(x => x.Add(It.IsAny<PlayerMissionStats>()), Times.Never);
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
    public async Task ComputeFinalFpsStats_ShouldComputeP1AndAverageFromData()
    {
        var fpsSamples = Enumerable.Range(1, 100).Select(i => i).ToList();
        var session = new MissionSession { SessionId = "session-1", PlayerPerformance = [new PlayerPerformance { Uid = "player1", Fps = fpsSamples }] };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        var existingStats = new PlayerMissionStats
        {
            MissionSessionId = "session-1",
            PlayerUid = "player1",
            FpsSampleCount = 100,
            FpsSampleSum = 5050
        };
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns(existingStats);

        var expectedUpdate = Builders<PlayerMissionStats>.Update.Set(x => x.FpsP1, 1).Set(x => x.FpsAverage, 50.5).RenderUpdate();

        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
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

        var existingStats = new PlayerMissionStats
        {
            MissionSessionId = "session-1",
            PlayerUid = "player1",
            FpsSampleCount = 4,
            FpsSampleSum = 190
        };
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns(existingStats);

        var expectedUpdate = Builders<PlayerMissionStats>.Update.Set(x => x.FpsP1, 40).Set(x => x.FpsAverage, 47.5).RenderUpdate();

        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
                               .Callback((Expression<Func<PlayerMissionStats, bool>> _, UpdateDefinition<PlayerMissionStats> update) => capturedUpdate = update)
                               .Returns(Task.CompletedTask);

        await _subject.ComputeFinalFpsStatsAsync("session-1");

        capturedUpdate.Should().NotBeNull();
        capturedUpdate.RenderUpdate().Should().BeEquivalentTo(expectedUpdate);
    }

    [Fact]
    public async Task HandlePerformanceEvent_WhenSamplesContainNegativeGaps_ShouldFilterThemFromRollingStats()
    {
        var session = new MissionSession
        {
            SessionId = "session-1",
            MissionStarted = DateTime.UtcNow.AddMinutes(-5),
            PlayerPerformance = []
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns((PlayerMissionStats)null);

        var players = new List<PlayerPerformance> { new() { Uid = "player1", Fps = [40, -10, 50] } };

        await _subject.HandlePerformanceEventAsync("session-1", [50], [], players);

        // Should only include the two valid samples (40, 50), not the gap (-10)
        _mockPlayerStatsContext.Verify(
            x => x.Add(It.Is<PlayerMissionStats>(s => s.FpsMin == 40 && s.FpsMax == 50 && s.FpsSampleCount == 2 && Math.Abs(s.FpsSampleSum - 90) < 0.01)),
            Times.Once
        );
    }

    [Fact]
    public async Task HandlePerformanceEvent_WhenAllSamplesAreNegative_ShouldNotCreateStats()
    {
        var session = new MissionSession
        {
            SessionId = "session-1",
            MissionStarted = DateTime.UtcNow.AddMinutes(-5),
            PlayerPerformance = []
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);

        var players = new List<PlayerPerformance> { new() { Uid = "player1", Fps = [-5, -10] } };

        await _subject.HandlePerformanceEventAsync("session-1", [50], [], players);

        _mockPlayerStatsContext.Verify(x => x.Add(It.IsAny<PlayerMissionStats>()), Times.Never);
        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Never
        );
    }

    [Fact]
    public async Task HandlePerformanceEvent_WhenDuplicateKeyOnAdd_ShouldFallThroughToUpdate()
    {
        var session = new MissionSession
        {
            SessionId = "session-1",
            MissionStarted = DateTime.UtcNow.AddMinutes(-5),
            PlayerPerformance = []
        };
        _mockSessionsContext.Setup(x => x.GetSingle(It.IsAny<Func<MissionSession, bool>>())).Returns(session);
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns((PlayerMissionStats)null);

        // Simulate duplicate key exception on Add using reflection (MongoWriteException has no public constructor in v3)
        var exception = CreateDuplicateKeyWriteException();
        _mockPlayerStatsContext.Setup(x => x.Add(It.IsAny<PlayerMissionStats>())).ThrowsAsync(exception);

        var players = new List<PlayerPerformance> { new() { Uid = "player1", Fps = [40, 50] } };

        await _subject.HandlePerformanceEventAsync("session-1", [50], [], players);

        // Should fall through to update after duplicate key
        _mockPlayerStatsContext.Verify(
            x => x.Update(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()),
            Times.Once
        );
    }

#pragma warning disable SYSLIB0050 // FormatterServices is the only way to construct MongoWriteException in tests (no public constructor in v3)
    private static MongoWriteException CreateDuplicateKeyWriteException()
    {
        // WriteError and MongoWriteException have no convenient public constructors in MongoDB.Driver v3,
        // so we use GetUninitializedObject and reflection to set the required fields.
        var writeError = (WriteError)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WriteError));
        var categoryField = typeof(WriteError).GetField("_category", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
                            typeof(WriteError).GetProperty("Category")
                                              ?.DeclaringType?.GetField(
                                                  "<Category>k__BackingField",
                                                  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                                              );
        categoryField?.SetValue(writeError, ServerErrorCategory.DuplicateKey);

        var mongoWriteException = (MongoWriteException)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MongoWriteException));
        var writeErrorField =
            typeof(MongoWriteException).GetField("_writeError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ??
            typeof(MongoWriteException).GetProperty("WriteError")
                                       ?.DeclaringType?.GetField(
                                           "<WriteError>k__BackingField",
                                           System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                                       );
        writeErrorField?.SetValue(mongoWriteException, writeError);

        return mongoWriteException;
    }
#pragma warning restore SYSLIB0050

    [Fact]
    public async Task ComputeFinalFpsStats_WhenStatsDocAbsent_ShouldCreateIt()
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
        _mockPlayerStatsContext.Setup(x => x.GetSingle(It.IsAny<Func<PlayerMissionStats, bool>>())).Returns((PlayerMissionStats)null);

        await _subject.ComputeFinalFpsStatsAsync("session-1");

        _mockPlayerStatsContext.Verify(
            x => x.Add(
                It.Is<PlayerMissionStats>(s => s.MissionSessionId == "session-1" &&
                                               s.PlayerUid == "player1" &&
                                               s.FpsP1 == 40 &&
                                               s.FpsAverage.HasValue &&
                                               Math.Abs(s.FpsAverage.Value) < 0.01 &&
                                               s.FpsMin == 40 &&
                                               s.FpsMax == 55
                )
            ),
            Times.Once
        );
    }
}
