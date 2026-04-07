using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services.StatsEventProcessors;

public class KillEventProcessorTests
{
    private readonly KillEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeKill()
    {
        _subject.EventType.Should().Be("kill");
    }

    [Fact]
    public void ProcessForPlayer_DirectKill_ShouldIncrementDirectKills()
    {
        var evt = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "infantry" },
            { "targetClassname", "O_Soldier_F" }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.Kills.Direct.Should().Be(1);
        stats.Kills.Indirect.Should().Be(0);
    }

    [Fact]
    public void ProcessForPlayer_IndirectKill_ShouldIncrementIndirectKills()
    {
        var evt = new BsonDocument
        {
            { "indirect", true },
            { "targetType", "vehicle" },
            { "targetClassname", "O_MRAP_02_F" }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.Kills.Direct.Should().Be(0);
        stats.Kills.Indirect.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldBucketKillsByTargetTypeWithCountAndClassnames()
    {
        var evtSoldier = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "infantry" },
            { "targetClassname", "O_Soldier_F" }
        };
        var evtOfficer = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "infantry" },
            { "targetClassname", "O_Officer_F" }
        };
        var evtMrap = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "vehicle" },
            { "targetClassname", "O_MRAP_02_F" }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtSoldier, stats);
        _subject.ProcessForPlayer(evtSoldier, stats);
        _subject.ProcessForPlayer(evtOfficer, stats);
        _subject.ProcessForPlayer(evtMrap, stats);

        var infantryBucket = stats.KillsByTargetType["infantry"];
        infantryBucket.Count.Should().Be(3);
        infantryBucket.Types["O_Soldier_F"].Should().Be(2);
        infantryBucket.Types["O_Officer_F"].Should().Be(1);

        var vehicleBucket = stats.KillsByTargetType["vehicle"];
        vehicleBucket.Count.Should().Be(1);
        vehicleBucket.Types["O_MRAP_02_F"].Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_WhenMissingFields_ShouldUseDefaults()
    {
        var evt = new BsonDocument();
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.Kills.Direct.Should().Be(1);
        var bucket = stats.KillsByTargetType["unknown"];
        bucket.Count.Should().Be(1);
        bucket.Types["unknown"].Should().Be(1);
    }

    [Theory]
    [InlineData("has.dot", "hasdot")]
    [InlineData("a.b.c", "abc")]
    [InlineData("$startsWithDollar", "startsWithDollar")]
    [InlineData("$$double", "double")]
    public void ProcessForPlayer_WhenClassnameHasInvalidMongoChars_ShouldStripThem(string classname, string expected)
    {
        var evt = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "infantry" },
            { "targetClassname", classname }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.KillsByTargetType["infantry"].Types[expected].Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("$")]
    public void ProcessForPlayer_WhenClassnameIsEmptyAfterStripping_ShouldBucketAsUnknown(string classname)
    {
        var evt = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "infantry" },
            { "targetClassname", classname }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.KillsByTargetType["infantry"].Types["unknown"].Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_WithWeaponAndAmmo_ShouldBucketKillsByWeapon()
    {
        var evtRifle1 = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "infantry" },
            { "targetClassname", "O_Soldier_F" },
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M855A1" }
        };
        var evtRifle2 = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "infantry" },
            { "targetClassname", "O_Officer_F" },
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "rhs_ammo_556x45_M856" }
        };
        var evtUgl = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "infantry" },
            { "targetClassname", "O_Soldier_F" },
            { "weapon", "rhs_weap_m4a1_m203" },
            { "ammo", "rhs_ammo_M433_HEDP" }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtRifle1, stats);
        _subject.ProcessForPlayer(evtRifle1, stats);
        _subject.ProcessForPlayer(evtRifle2, stats);
        _subject.ProcessForPlayer(evtUgl, stats);

        var rifle = stats.KillsByWeapon["rhs_weap_m4a1"];
        rifle.Count.Should().Be(3);
        rifle.Ammo["rhs_ammo_556x45_M855A1"].Should().Be(2);
        rifle.Ammo["rhs_ammo_556x45_M856"].Should().Be(1);

        var ugl = stats.KillsByWeapon["rhs_weap_m4a1_m203"];
        ugl.Count.Should().Be(1);
        ugl.Ammo["rhs_ammo_M433_HEDP"].Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_WithEmptyWeapon_ShouldNotBucketKillsByWeapon()
    {
        var evt = new BsonDocument
        {
            { "indirect", true },
            { "targetType", "infantry" },
            { "targetClassname", "O_Soldier_F" },
            { "weapon", "" },
            { "ammo", "" }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.Kills.Indirect.Should().Be(1);
        stats.KillsByWeapon.Should().BeEmpty();
    }

    [Fact]
    public void ProcessForPlayer_WithWeaponButEmptyAmmo_ShouldBucketAsUnknownAmmo()
    {
        var evt = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "infantry" },
            { "targetClassname", "O_Soldier_F" },
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "" }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.KillsByWeapon["rhs_weap_m4a1"].Count.Should().Be(1);
        stats.KillsByWeapon["rhs_weap_m4a1"].Ammo["unknown"].Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_StructureKill_ShouldTrackAsStructure()
    {
        var evt = new BsonDocument
        {
            { "indirect", false },
            { "targetType", "structure" },
            { "targetClassname", "Land_Cargo_HQ_V1_F" }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.Kills.Direct.Should().Be(1);
        var bucket = stats.KillsByTargetType["structure"];
        bucket.Count.Should().Be(1);
        bucket.Types["Land_Cargo_HQ_V1_F"].Should().Be(1);
    }
}
