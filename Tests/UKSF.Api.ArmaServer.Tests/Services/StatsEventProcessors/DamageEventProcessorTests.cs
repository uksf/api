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
    public void EventType_ShouldBeCombatDamage()
    {
        _subject.EventType.Should().Be("combatDamage");
    }

    [Fact]
    public void ProcessForPlayer_ShouldAccumulateTotalDamageDealt()
    {
        var evt1 = new BsonDocument { { "damage", 0.35 }, { "damageType", "B_556x45_Ball" } };
        var evt2 = new BsonDocument { { "damage", 0.15 }, { "damageType", "B_556x45_Ball" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        stats.TotalDamageDealt.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void ProcessForPlayer_ShouldAccumulateDamageDealtByAmmo()
    {
        var evt1 = new BsonDocument { { "damage", 0.35 }, { "damageType", "B_556x45_Ball" } };
        var evt2 = new BsonDocument { { "damage", 0.20 }, { "damageType", "B_556x45_Ball" } };
        var evt3 = new BsonDocument { { "damage", 0.40 }, { "damageType", "B_762x51_Ball" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);
        _subject.ProcessForPlayer(evt3, stats);

        stats.DamageDealtByAmmo["B_556x45_Ball"].Should().BeApproximately(0.55, 0.001);
        stats.DamageDealtByAmmo["B_762x51_Ball"].Should().BeApproximately(0.40, 0.001);
    }

    [Fact]
    public void ProcessForPlayer_WhenMissingDamageType_ShouldBucketAsUnknown()
    {
        var evt = new BsonDocument { { "damage", 0.25 } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.DamageDealtByAmmo["unknown"].Should().BeApproximately(0.25, 0.001);
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
