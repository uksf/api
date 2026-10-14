using System;
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

public class MissionStatsServiceEndBeforeStartTests
{
    private static readonly DateTime Ended = new(2026, 10, 14, 20, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMissionSessionsContext> _sessions = new();
    private readonly Mock<IPerformanceService> _performance = new();
    private readonly MissionStatsService _subject;
    private MissionSession _stored;

    public MissionStatsServiceEndBeforeStartTests()
    {
        _sessions.Setup(x => x.FindFirst(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<Expression<Func<MissionSession, object>>>()))
                 .Returns(() => _stored);
        _subject = new MissionStatsService(
            _sessions.Object,
            Mock.Of<IRawEventStore>(),
            Mock.Of<IPlayerMissionStatsContext>(),
            Mock.Of<IMissionStatsContext>(),
            _performance.Object,
            Mock.Of<IUksfLogger>()
        );
    }

    [Fact]
    public async Task MissionEnded_WithNoSessionYet_RecordsTheEndSoALateStartSeesIt()
    {
        BsonDocument written = null;
        _sessions.Setup(x => x.Upsert(It.IsAny<Expression<Func<MissionSession, bool>>>(), It.IsAny<UpdateDefinition<MissionSession>>()))
                 .Callback<Expression<Func<MissionSession, bool>>,
                     UpdateDefinition<MissionSession>>((_, update) => written = update.RenderUpdate().AsBsonDocument)
                 .Returns(Task.CompletedTask);

        await _subject.HandleMissionEndedAsync("A", 600, Ended);

        written["$set"]["missionEnded"].ToUniversalTime().Should().Be(Ended);
        written["$setOnInsert"]["sessionId"].AsString.Should().Be("A");
        _performance.Verify(x => x.ComputeFinalFpsStatsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StatisticsBatch_ForAnEndedSessionWithoutMissionName_FillsItIn()
    {
        _stored = new MissionSession
        {
            Id = "id-1",
            SessionId = "A",
            MissionEnded = Ended
        };
        BsonDocument written = null;
        _sessions.Setup(x => x.Update("id-1", It.IsAny<UpdateDefinition<MissionSession>>()))
                 .Callback<string, UpdateDefinition<MissionSession>>((_, update) => written = update.RenderUpdate().AsBsonDocument)
                 .Returns(Task.CompletedTask);

        var session = await _subject.GetOrCreateSessionAsync("A", "op", "Altis", Ended.AddMinutes(-1));

        written["$set"]["mission"].AsString.Should().Be("op");
        written["$set"]["map"].AsString.Should().Be("Altis");
        session.Mission.Should().Be("op");
    }
}
