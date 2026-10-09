using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
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

    private void SetupClaims(params bool[] results)
    {
        var sequence = _sessions.SetupSequence(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()));
        foreach (var result in results)
        {
            sequence = sequence.ReturnsAsync(result);
        }
    }

    private void SetupRunningSession(DateTime lastBatch)
    {
        _sessions.Setup(x => x.FindFirst(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<Expression<Func<MissionSession, object>>>()))
                 .Returns(() => RunningSession(lastBatch));
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_WhenAnotherPathClaimedTheSessionFirst_DoesNotBackfillOrComputeFps()
    {
        SetupRunningSession(new DateTime(2026, 10, 7, 20, 30, 0, DateTimeKind.Utc));
        SetupClaims(false);

        await _subject.FinaliseKilledSessionAsync("session-1");

        _rawEventStore.Verify(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<List<BsonDocument>>()), Times.Never);
        _performance.Verify(x => x.ComputeFinalFpsStatsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FinaliseKilledSessionAsync_WhenTwoCallersComputeTheSameEndTimestamp_OnlyTheClaimWinnerBackfills()
    {
        var lastBatch = new DateTime(2026, 10, 7, 20, 30, 0, DateTimeKind.Utc);
        var stored = RunningSession(lastBatch);
        var reads = 0;
        TaskCompletionSource bothCallersHaveRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _sessions.Setup(x => x.FindFirst(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<Expression<Func<MissionSession, object>>>()))
                 .Returns(() =>
                     {
                         if (Interlocked.Increment(ref reads) == 2)
                         {
                             bothCallersHaveRead.SetResult();
                         }

                         var copy = RunningSession(lastBatch);
                         copy.MissionEnded = stored.MissionEnded;
                         return copy;
                     }
                 );
        _sessions.Setup(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                 .Returns(async () =>
                     {
                         await bothCallersHaveRead.Task;
                         lock (stored)
                         {
                             if (stored.MissionEnded is not null)
                             {
                                 return false;
                             }

                             stored.MissionEnded = lastBatch;
                             return true;
                         }
                     }
                 );

        await Task.WhenAll(_subject.FinaliseKilledSessionAsync("session-1"), _subject.FinaliseKilledSessionAsync("session-1"));

        _rawEventStore.Verify(x => x.StoreAsync("session-1", It.IsAny<List<BsonDocument>>()), Times.Once);
        _performance.Verify(x => x.ComputeFinalFpsStatsAsync("session-1"), Times.Once);
    }
}
