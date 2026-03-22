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
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "head" },
            { "targetType", "infantry" },
            { "distance2D", 150 },
            { "distance3D", 155 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TotalHits.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackBodyPartHitsInAmmoBreakdown()
    {
        var evtHead = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "head" },
            { "targetType", "infantry" },
            { "distance2D", 100 },
            { "distance3D", 100 }
        };
        var evtTorso = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "torso" },
            { "targetType", "infantry" },
            { "distance2D", 200 },
            { "distance3D", 200 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtHead, stats);
        _subject.ProcessForPlayer(evtHead, stats);
        _subject.ProcessForPlayer(evtTorso, stats);

        var ammoStats = stats.WeaponBreakdown["rhs_weap_m4a1"].AmmoBreakdown["rhs_ammo_556x45_M855A1"];
        ammoStats.BodyPartHits.Should().ContainKey("head").WhoseValue.Should().Be(2);
        ammoStats.BodyPartHits.Should().ContainKey("torso").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldAlsoTrackTopLevelBodyPartHits()
    {
        var evt = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "head" },
            { "targetType", "infantry" },
            { "distance2D", 100 },
            { "distance3D", 100 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.BodyPartHits.Should().ContainKey("head").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackEngagementDistancePerAmmo()
    {
        var evt1 = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "head" },
            { "targetType", "infantry" },
            { "distance2D", 150 },
            { "distance3D", 155 }
        };
        var evt2 = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "torso" },
            { "targetType", "infantry" },
            { "distance2D", 300 },
            { "distance3D", 310 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        var ammoStats = stats.WeaponBreakdown["rhs_weap_m4a1"].AmmoBreakdown["rhs_ammo_556x45_M855A1"];
        ammoStats.Hits.Should().Be(2);
        ammoStats.TotalEngagementDistance2D.Should().Be(450);
        ammoStats.TotalEngagementDistance3D.Should().Be(465);
        ammoStats.MaxEngagementDistance2D.Should().Be(300);

        // Weapon-level totals should also be maintained
        var weaponStats = stats.WeaponBreakdown["rhs_weap_m4a1"];
        weaponStats.Hits.Should().Be(2);
        weaponStats.TotalEngagementDistance2D.Should().Be(450);
        weaponStats.MaxEngagementDistance2D.Should().Be(300);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackHitsByTargetType()
    {
        var evtInfantry = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "torso" },
            { "targetType", "infantry" },
            { "distance2D", 100 },
            { "distance3D", 100 }
        };
        var evtVehicle = new BsonDocument
        {
            { "weapon", "launch_RPG32_F" },
            { "ammo", "rhs_ammo_pg32v" },
            { "bodyPart", "" },
            { "targetType", "vehicle" },
            { "distance2D", 500 },
            { "distance3D", 500 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtInfantry, stats);
        _subject.ProcessForPlayer(evtInfantry, stats);
        _subject.ProcessForPlayer(evtVehicle, stats);

        stats.HitsByTargetType.Should().ContainKey("infantry").WhoseValue.Should().Be(2);
        stats.HitsByTargetType.Should().ContainKey("vehicle").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_WhenMissingFields_ShouldUseDefaults()
    {
        var evt = new BsonDocument();
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TotalHits.Should().Be(1);
        stats.WeaponBreakdown.Should().ContainKey("unknown");
        stats.WeaponBreakdown["unknown"].AmmoBreakdown.Should().ContainKey("unknown");
        stats.HitsByTargetType.Should().ContainKey("unknown");
    }

    [Fact]
    public void ProcessForPlayer_WhenEmptyBodyPart_ShouldNotTrackBodyPart()
    {
        var evt = new BsonDocument
        {
            { "weapon", "launch_RPG32_F" },
            { "ammo", "rhs_ammo_pg32v" },
            { "bodyPart", "" },
            { "targetType", "vehicle" },
            { "distance2D", 500 },
            { "distance3D", 500 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.BodyPartHits.Should().BeEmpty();
        stats.WeaponBreakdown["launch_RPG32_F"].AmmoBreakdown["rhs_ammo_pg32v"].BodyPartHits.Should().BeEmpty();
    }
}
