using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class MissionStatsServiceHealthTests
{
    static MissionStatsServiceHealthTests()
    {
        ConventionRegistry.Register("TestCamelCase", new ConventionPack { new CamelCaseElementNameConvention() }, _ => true);
    }

    private readonly Mock<IPlayerMissionStatsContext> _mockPlayerStatsContext = new();
    private readonly MissionStatsService _subject;

    public MissionStatsServiceHealthTests()
    {
        _subject = new MissionStatsService(
            new Mock<IMissionSessionsContext>().Object,
            new Mock<IRawEventStore>().Object,
            _mockPlayerStatsContext.Object,
            new Mock<IMissionStatsContext>().Object,
            new Mock<IPerformanceService>().Object,
            new Mock<IUksfLogger>().Object
        );
    }

    [Fact]
    public async Task UpdatePlayerStatsAsync_IncrementsEveryHealthCounter()
    {
        UpdateDefinition<PlayerMissionStats> capturedUpdate = null;
        _mockPlayerStatsContext.Setup(x => x.Upsert(It.IsAny<Expression<Func<PlayerMissionStats, bool>>>(), It.IsAny<UpdateDefinition<PlayerMissionStats>>()))
                               .Callback<Expression<Func<PlayerMissionStats, bool>>, UpdateDefinition<PlayerMissionStats>>((_, u) => capturedUpdate = u)
                               .Returns(Task.CompletedTask);

        await _subject.UpdatePlayerStatsAsync(
            "session-123",
            "76561198012345678",
            new PlayerMissionStats
            {
                TimesUnconscious = 1,
                TimesInCardiacArrest = 2,
                TimesInComa = 3
            }
        );

        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<PlayerMissionStats>();
        var rendered = capturedUpdate.Render(new RenderArgs<PlayerMissionStats>(serializer, BsonSerializer.SerializerRegistry));
        var incDoc = rendered["$inc"].AsBsonDocument;
        incDoc["timesUnconscious"].AsInt32.Should().Be(1);
        incDoc["timesInCardiacArrest"].AsInt32.Should().Be(2);
        incDoc["timesInComa"].AsInt32.Should().Be(3);
    }
}
