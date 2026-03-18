using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services.StatsEventProcessors;

public class DamageReceivedEventProcessorTests
{
    private readonly DamageReceivedEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeDamageReceived()
    {
        _subject.EventType.Should().Be("damageReceived");
    }

    [Fact]
    public void ProcessForPlayer_ShouldIncrementTimesWounded()
    {
        var evt = new BsonDocument { { "bodyParts", new BsonArray { "torso" } }, { "damageType", "BulletCore" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);
        _subject.ProcessForPlayer(evt, stats);

        stats.TimesWounded.Should().Be(2);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackWoundsByBodyPart()
    {
        var evt = new BsonDocument { { "bodyParts", new BsonArray { "torso", "legs" } }, { "damageType", "BulletCore" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.WoundsByBodyPart.Should().ContainKey("torso").WhoseValue.Should().Be(1);
        stats.WoundsByBodyPart.Should().ContainKey("legs").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackWoundsByDamageType()
    {
        var evtBullet = new BsonDocument { { "bodyParts", new BsonArray { "torso" } }, { "damageType", "BulletCore" } };
        var evtExplosion = new BsonDocument { { "bodyParts", new BsonArray { "legs" } }, { "damageType", "GrenadeCore" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtBullet, stats);
        _subject.ProcessForPlayer(evtBullet, stats);
        _subject.ProcessForPlayer(evtExplosion, stats);

        stats.WoundsByDamageType.Should().ContainKey("BulletCore").WhoseValue.Should().Be(2);
        stats.WoundsByDamageType.Should().ContainKey("GrenadeCore").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_WhenMissingBodyParts_ShouldStillCountWound()
    {
        var evt = new BsonDocument { { "damageType", "collision" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TimesWounded.Should().Be(1);
        stats.WoundsByBodyPart.Should().BeEmpty();
        stats.WoundsByDamageType.Should().ContainKey("collision");
    }
}
