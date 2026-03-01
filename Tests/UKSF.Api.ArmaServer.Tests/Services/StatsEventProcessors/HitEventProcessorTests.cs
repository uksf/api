using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services.StatsEventProcessors;

public class HitEventProcessorTests
{
    private readonly HitEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeHit()
    {
        _subject.EventType.Should().Be("hit");
    }

    [Fact]
    public void ProcessForPlayer_ShouldIncrementTotalHits()
    {
        var evt = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "bodyPart", "head" },
            { "distance", 150 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TotalHits.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackBodyPartHits()
    {
        var evtHead = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "bodyPart", "head" },
            { "distance", 100 }
        };
        var evtTorso = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "bodyPart", "torso" },
            { "distance", 200 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtHead, stats);
        _subject.ProcessForPlayer(evtHead, stats);
        _subject.ProcessForPlayer(evtTorso, stats);

        stats.BodyPartHits.Should().ContainKey("head").WhoseValue.Should().Be(2);
        stats.BodyPartHits.Should().ContainKey("torso").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackWeaponHits()
    {
        var evt = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "bodyPart", "head" },
            { "distance", 100 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);
        _subject.ProcessForPlayer(evt, stats);

        stats.WeaponBreakdown.Should().ContainKey("rhs_weap_m4a1");
        stats.WeaponBreakdown["rhs_weap_m4a1"].Hits.Should().Be(2);
    }

    [Fact]
    public void ProcessForPlayer_ShouldAccumulateDistance()
    {
        var evt1 = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "bodyPart", "head" },
            { "distance", 150 }
        };
        var evt2 = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "bodyPart", "torso" },
            { "distance", 300 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        stats.TotalDistance.Should().Be(450);
        stats.TotalHits.Should().Be(2);
    }

    [Fact]
    public void ProcessForPlayer_WhenMissingFields_ShouldUseDefaults()
    {
        var evt = new BsonDocument();
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TotalHits.Should().Be(1);
        stats.WeaponBreakdown.Should().ContainKey("unknown");
        stats.BodyPartHits.Should().ContainKey("unknown");
        stats.TotalDistance.Should().Be(0);
    }
}
