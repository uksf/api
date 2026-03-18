using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services.StatsEventProcessors;

public class DamageEventProcessorTests
{
    private readonly DamageEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeDamage()
    {
        _subject.EventType.Should().Be("damage");
    }

    [Fact]
    public void ProcessForPlayer_ShouldAccumulateTotalDamageDealt()
    {
        var evt1 = new BsonDocument { { "totalDamage", 0.35 } };
        var evt2 = new BsonDocument { { "totalDamage", 0.15 } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        stats.TotalDamageDealt.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void ProcessForPlayer_WhenMissingFields_ShouldDefaultToZero()
    {
        var evt = new BsonDocument();
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TotalDamageDealt.Should().Be(0);
    }
}
