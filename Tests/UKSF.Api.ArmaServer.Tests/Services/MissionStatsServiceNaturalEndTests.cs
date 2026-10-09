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
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class MissionStatsServiceNaturalEndTests
{
    private static readonly DateTime MissionStarted = new(2026, 10, 9, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastBatch = new(2026, 10, 9, 20, 29, 0, DateTimeKind.Utc);
    private static readonly DateTime NaturalEnd = new(2026, 10, 9, 20, 30, 0, DateTimeKind.Utc);
    private const double NaturalDuration = 5400;

    private readonly Mock<IMissionSessionsContext> _sessions = new();
    private readonly Mock<IRawEventStore> _rawEventStore = new();
    private readonly Mock<IPerformanceService> _performance = new();
    private readonly MissionStatsService _subject;
    private readonly MissionSession _stored = new()
    {
        Id = "id-1",
        SessionId = "session-1",
        MissionStarted = MissionStarted,
        LastBatchReceived = LastBatch,
        PlayerPresence = []
    };

    public MissionStatsServiceNaturalEndTests()
    {
        _sessions.Setup(x => x.FindFirst(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<Expression<Func<MissionSession, object>>>()))
                 .Returns(() => Snapshot());
        _sessions.Setup(x => x.FindAndUpdate(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                 .Returns<Expression<Func<MissionSession, bool>>, UpdateDefinition<MissionSession>>((_, update) => Task.FromResult(ClaimAndApply(update)));
        _sessions.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                 .Returns<string, UpdateDefinition<MissionSession>>((_, update) =>
                     {
                         Apply(update);
                         return Task.CompletedTask;
                     }
                 );
        _subject = new MissionStatsService(
            _sessions.Object,
            _rawEventStore.Object,
            new Mock<IPlayerMissionStatsContext>().Object,
            new Mock<IMissionStatsContext>().Object,
            _performance.Object,
            new Mock<IUksfLogger>().Object
        );
    }

    private MissionSession Snapshot()
    {
        lock (_stored)
        {
            return new MissionSession
            {
                Id = _stored.Id,
                SessionId = _stored.SessionId,
                MissionStarted = _stored.MissionStarted,
                LastBatchReceived = _stored.LastBatchReceived,
                MissionEnded = _stored.MissionEnded,
                DurationSeconds = _stored.DurationSeconds,
                PlayerPresence = []
            };
        }
    }

    private bool ClaimAndApply(UpdateDefinition<MissionSession> update)
    {
        lock (_stored)
        {
            if (_stored.MissionEnded is not null)
            {
                return false;
            }

            Apply(update);
            return true;
        }
    }

    private void Apply(UpdateDefinition<MissionSession> update)
    {
        lock (_stored)
        {
            foreach (var field in update.RenderUpdate().AsBsonDocument["$set"].AsBsonDocument)
            {
                if (field.Name.Equals("missionEnded", StringComparison.OrdinalIgnoreCase))
                {
                    _stored.MissionEnded = field.Value.ToUniversalTime();
                }
                else if (field.Name.Equals("durationSeconds", StringComparison.OrdinalIgnoreCase))
                {
                    _stored.DurationSeconds = field.Value.ToDouble();
                }
            }
        }
    }

    [Fact]
    public async Task NaturalEndThenKilledFinalisation_KeepsTheNaturalRecordAndSkipsTheBackfill()
    {
        await _subject.HandleMissionEndedAsync("session-1", NaturalDuration, NaturalEnd);
        await _subject.FinaliseKilledSessionAsync("session-1");

        _stored.MissionEnded.Should().Be(NaturalEnd);
        _stored.DurationSeconds.Should().Be(NaturalDuration);
        _rawEventStore.Verify(x => x.StoreAsync(It.IsAny<string>(), It.IsAny<List<BsonDocument>>()), Times.Never);
        _performance.Verify(x => x.ComputeFinalFpsStatsAsync("session-1"), Times.Once);
    }

    [Fact]
    public async Task KilledFinalisationThenLateNaturalEnd_TheNaturalRecordWinsWithoutRepeatingFinalisation()
    {
        await _subject.FinaliseKilledSessionAsync("session-1");
        _stored.MissionEnded.Should().Be(LastBatch);

        await _subject.HandleMissionEndedAsync("session-1", NaturalDuration, NaturalEnd);

        _stored.MissionEnded.Should().Be(NaturalEnd);
        _stored.DurationSeconds.Should().Be(NaturalDuration);
        _rawEventStore.Verify(x => x.StoreAsync("session-1", It.IsAny<List<BsonDocument>>()), Times.Once);
        _performance.Verify(x => x.ComputeFinalFpsStatsAsync("session-1"), Times.Once);
    }

    [Fact]
    public async Task DuplicateNaturalEnds_ComputeFinalFpsOnce()
    {
        await _subject.HandleMissionEndedAsync("session-1", NaturalDuration, NaturalEnd);
        await _subject.HandleMissionEndedAsync("session-1", NaturalDuration, NaturalEnd);

        _stored.MissionEnded.Should().Be(NaturalEnd);
        _performance.Verify(x => x.ComputeFinalFpsStatsAsync("session-1"), Times.Once);
    }
}
