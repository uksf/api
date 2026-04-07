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
            { "distance2D", 150 }
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
            { "distance2D", 100 }
        };
        var evtTorso = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "torso" },
            { "targetType", "infantry" },
            { "distance2D", 200 }
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
            { "distance2D", 100 }
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
            { "distance2D", 150 }
        };
        var evt2 = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "torso" },
            { "targetType", "infantry" },
            { "distance2D", 300 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        var ammoStats = stats.WeaponBreakdown["rhs_weap_m4a1"].AmmoBreakdown["rhs_ammo_556x45_M855A1"];
        ammoStats.Hits.Should().Be(2);
        ammoStats.EngagementDistanceSum.Should().Be(450);
        ammoStats.MinEngagementDistance.Should().Be(150);
        ammoStats.MaxEngagementDistance.Should().Be(300);

        // Weapon-level totals should also be maintained
        var weaponStats = stats.WeaponBreakdown["rhs_weap_m4a1"];
        weaponStats.Hits.Should().Be(2);
        weaponStats.EngagementDistanceSum.Should().Be(450);
        weaponStats.MinEngagementDistance.Should().Be(150);
        weaponStats.MaxEngagementDistance.Should().Be(300);
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
            { "distance2D", 100 }
        };
        var evtVehicle = new BsonDocument
        {
            { "weapon", "launch_RPG32_F" },
            { "ammo", "rhs_ammo_pg32v" },
            { "bodyPart", "" },
            { "targetType", "vehicle" },
            { "distance2D", 500 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtInfantry, stats);
        _subject.ProcessForPlayer(evtInfantry, stats);
        _subject.ProcessForPlayer(evtVehicle, stats);

        stats.HitsByTargetType.Should().ContainKey("infantry").WhoseValue.Should().Be(2);
        stats.HitsByTargetType.Should().ContainKey("vehicle").WhoseValue.Should().Be(1);
    }

    [Theory]
    [InlineData("ballistic")]
    [InlineData("explosive")]
    [InlineData("other")]
    public void ProcessForPlayer_ShouldBucketHitsByCategory(string category)
    {
        var evt = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "any" },
            { "category", category },
            { "targetType", "infantry" },
            { "distance2D", 100 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        var (ballistic, explosive, other) = (stats.BallisticHits, stats.ExplosiveHits, stats.OtherHits);
        (ballistic + explosive + other).Should().Be(1);
        switch (category)
        {
            case "ballistic": ballistic.Should().Be(1); break;
            case "explosive": explosive.Should().Be(1); break;
            default:          other.Should().Be(1); break;
        }
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
            { "distance2D", 500 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.BodyPartHits.Should().BeEmpty();
        stats.WeaponBreakdown["launch_RPG32_F"].AmmoBreakdown["rhs_ammo_pg32v"].BodyPartHits.Should().BeEmpty();
    }

    [Fact]
    public void ProcessForPlayer_ShouldIsolateStatsPerAmmoType()
    {
        var evtAmmo1 = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" },
            { "bodyPart", "head" },
            { "targetType", "infantry" },
            { "distance2D", 200 }
        };
        var evtAmmo2 = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M856" },
            { "bodyPart", "torso" },
            { "targetType", "infantry" },
            { "distance2D", 400 }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtAmmo1, stats);
        _subject.ProcessForPlayer(evtAmmo2, stats);

        var ammo1 = stats.WeaponBreakdown["rhs_weap_m4a1"].AmmoBreakdown["rhs_ammo_556x45_M855A1"];
        ammo1.Hits.Should().Be(1);
        ammo1.BodyPartHits.Should().ContainKey("head").WhoseValue.Should().Be(1);
        ammo1.BodyPartHits.Should().NotContainKey("torso");
        ammo1.MaxEngagementDistance.Should().Be(200);

        var ammo2 = stats.WeaponBreakdown["rhs_weap_m4a1"].AmmoBreakdown["rhs_ammo_556x45_M856"];
        ammo2.Hits.Should().Be(1);
        ammo2.BodyPartHits.Should().ContainKey("torso").WhoseValue.Should().Be(1);
        ammo2.BodyPartHits.Should().NotContainKey("head");
        ammo2.MaxEngagementDistance.Should().Be(400);

        // Weapon-level should have both
        var weapon = stats.WeaponBreakdown["rhs_weap_m4a1"];
        weapon.Hits.Should().Be(2);
        weapon.MinEngagementDistance.Should().Be(200);
        weapon.MaxEngagementDistance.Should().Be(400);
    }
}
