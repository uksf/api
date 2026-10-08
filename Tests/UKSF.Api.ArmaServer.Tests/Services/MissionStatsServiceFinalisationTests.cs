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
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class MissionStatsServiceFinalisationTests
{
    private readonly Mock<IMissionSessionsContext> _sessions = new();
    private readonly Mock<IRawEventStore> _rawEventStore = new();
    private readonly Mock<IPerformanceService> _performance = new();
    private readonly MissionStatsService _subject;

    public MissionStatsServiceFinalisationTests()
    {
        _subject = new MissionStatsService(
            _sessions.Object,
            _rawEventStore.Object,
            new Mock<IPlayerMissionStatsContext>().Object,
            new Mock<IMissionStatsContext>().Object,
            _performance.Object,
            new Mock<IUksfLogger>().Object
        );
    }

    private static MissionSession RunningSession(DateTime lastBatch)
    {
        return new MissionSession
        {
            Id = "id-1",
            SessionId = "session-1",
            MissionStarted = lastBatch.AddMinutes(-30),
            LastBatchReceived = lastBatch,
            PlayerPresence = []
        };
    }

    private void SetupReads(MissionSession beforeClaim, MissionSession afterClaim)
    {
        _sessions.SetupSequence(x => x.FindFirst(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<Expression<Func<MissionSession, object>>>()))
                 .Returns(beforeClaim)
                 .Returns(afterClaim);
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_WhenAnotherPathClaimedTheSessionFirst_DoesNotBackfillOrComputeFps()
    {
        var lastBatch = new DateTime(2026, 10, 7, 20, 30, 0, DateTimeKind.Utc);
        var claimedByOther = RunningSession(lastBatch);
        claimedByOther.MissionEnded = lastBatch.AddSeconds(-5);
        SetupReads(RunningSession(lastBatch), claimedByOther);

        await _subject.FinaliseKilledSessionAsync("session-1");

        _rawEventStore.Verify(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<List<BsonDocument>>()), Times.Never);
        _performance.Verify(x => x.ComputeFinalFpsStatsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_WithSubMillisecondLastBatch_WinsTheClaimAfterMongoTruncatesTheTimestamp()
    {
        var lastBatch = new DateTime(2026, 10, 7, 20, 30, 0, DateTimeKind.Utc).AddTicks(1234);
        var storedByMongo = RunningSession(lastBatch);
        storedByMongo.MissionEnded = new DateTime(2026, 10, 7, 20, 30, 0, DateTimeKind.Utc);
        SetupReads(RunningSession(lastBatch), storedByMongo);

        await _subject.FinaliseKilledSessionAsync("session-1");

        _sessions.Verify(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()), Times.Once);
        _rawEventStore.Verify(x => x.StoreAsync("session-1", It.IsAny<List<BsonDocument>>()), Times.Once);
        _performance.Verify(x => x.ComputeFinalFpsStatsAsync("session-1"), Times.Once);
    }
}
