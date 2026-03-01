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
        var evt = new BsonDocument { { "weapon", "rhs_weap_m4a1" }, { "fireMode", "Single" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TotalShots.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackWeaponShots()
    {
        var evt = new BsonDocument { { "weapon", "rhs_weap_m4a1" }, { "fireMode", "Single" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);
        _subject.ProcessForPlayer(evt, stats);

        stats.WeaponBreakdown.Should().ContainKey("rhs_weap_m4a1");
        stats.WeaponBreakdown["rhs_weap_m4a1"].Shots.Should().Be(2);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackFireModesPerWeapon()
    {
        var evtSingle = new BsonDocument { { "weapon", "rhs_weap_m4a1" }, { "fireMode", "Single" } };
        var evtAuto = new BsonDocument { { "weapon", "rhs_weap_m4a1" }, { "fireMode", "FullAuto" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtSingle, stats);
        _subject.ProcessForPlayer(evtAuto, stats);
        _subject.ProcessForPlayer(evtAuto, stats);

        var fireModes = stats.WeaponBreakdown["rhs_weap_m4a1"].FireModes;
        fireModes.Should().ContainKey("Single").WhoseValue.Should().Be(1);
        fireModes.Should().ContainKey("FullAuto").WhoseValue.Should().Be(2);
    }

    [Fact]
    public void ProcessForPlayer_WhenMissingFields_ShouldUseDefaults()
    {
        var evt = new BsonDocument();
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.TotalShots.Should().Be(1);
        stats.WeaponBreakdown.Should().ContainKey("unknown");
        stats.WeaponBreakdown["unknown"].FireModes.Should().ContainKey("unknown");
    }

    [Fact]
    public void ProcessForMission_ShouldIncrementEventCount()
    {
        var evt = new BsonDocument();
        var stats = new MissionStats();

        _subject.ProcessForMission(evt, stats);
        _subject.ProcessForMission(evt, stats);

        stats.EventCounts.Should().ContainKey("shot").WhoseValue.Should().Be(2);
    }
}
