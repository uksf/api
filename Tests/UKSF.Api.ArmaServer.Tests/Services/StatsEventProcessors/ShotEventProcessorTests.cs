using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services.StatsEventProcessors;

public class ShotEventProcessorTests
{
    private readonly ShotEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeShot()
    {
        _subject.EventType.Should().Be("shot");
    }

    [Fact]
    public void ProcessForPlayer_ShouldIncrementTotalShots()
    {
        var evt = new BsonDocument { { "weapon", "rhs_weap_m4a1" }, { "ammo", "rhs_ammo_556x45_M855A1" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TotalShots.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackWeaponShots()
    {
        var evt = new BsonDocument { { "weapon", "rhs_weap_m4a1" }, { "ammo", "rhs_ammo_556x45_M855A1" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);
        _subject.ProcessForPlayer(evt, stats);

        stats.WeaponBreakdown.Should().ContainKey("rhs_weap_m4a1");
        stats.WeaponBreakdown["rhs_weap_m4a1"].Shots.Should().Be(2);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackAmmoBreakdownPerWeapon()
    {
        var evtM855 = new BsonDocument { { "weapon", "rhs_weap_m4a1" }, { "ammo", "rhs_ammo_556x45_M855A1" } };
        var evtTracer = new BsonDocument { { "weapon", "rhs_weap_m4a1" }, { "ammo", "rhs_ammo_556x45_M856" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtM855, stats);
        _subject.ProcessForPlayer(evtTracer, stats);
        _subject.ProcessForPlayer(evtTracer, stats);

        var ammoBreakdown = stats.WeaponBreakdown["rhs_weap_m4a1"].AmmoBreakdown;
        ammoBreakdown.Should().ContainKey("rhs_ammo_556x45_M855A1");
        ammoBreakdown["rhs_ammo_556x45_M855A1"].Shots.Should().Be(1);
        ammoBreakdown.Should().ContainKey("rhs_ammo_556x45_M856");
        ammoBreakdown["rhs_ammo_556x45_M856"].Shots.Should().Be(2);
    }

    [Theory]
    [InlineData("ballistic")]
    [InlineData("explosive")]
    [InlineData("other")]
    public void ProcessForPlayer_ShouldBucketShotsByCategory(string category)
    {
        var evt = new BsonDocument
        {
            { "weapon", "rhs_weap_m4a1" },
            { "ammo", "any" },
            { "category", category }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);
        _subject.ProcessForPlayer(evt, stats);

        var (ballistic, explosive, other) = (stats.BallisticShots, stats.ExplosiveShots, stats.OtherShots);
        (ballistic + explosive + other).Should().Be(2);
        switch (category)
        {
            case "ballistic": ballistic.Should().Be(2); break;
            case "explosive": explosive.Should().Be(2); break;
            default:          other.Should().Be(2); break;
        }
    }

    [Fact]
    public void ProcessForPlayer_WhenCategoryMissing_ShouldBucketAsOther()
    {
        var evt = new BsonDocument { { "weapon", "rhs_weap_m4a1" }, { "ammo", "any" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.OtherShots.Should().Be(1);
        stats.BallisticShots.Should().Be(0);
        stats.ExplosiveShots.Should().Be(0);
    }

    [Fact]
    public void ProcessForPlayer_WhenMissingFields_ShouldUseDefaults()
    {
        var evt = new BsonDocument();
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TotalShots.Should().Be(1);
        stats.WeaponBreakdown.Should().ContainKey("unknown");
        stats.WeaponBreakdown["unknown"].AmmoBreakdown.Should().ContainKey("unknown");
    }
}
