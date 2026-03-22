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
        var evt = new BsonDocument { { "indirect", false }, { "targetType", "infantry" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.Kills.Direct.Should().Be(1);
        stats.Kills.Indirect.Should().Be(0);
    }

    [Fact]
    public void ProcessForPlayer_IndirectKill_ShouldIncrementIndirectKills()
    {
        var evt = new BsonDocument { { "indirect", true }, { "targetType", "vehicle" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.Kills.Direct.Should().Be(0);
        stats.Kills.Indirect.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackKillsByTargetType()
    {
        var evtInfantry = new BsonDocument { { "indirect", false }, { "targetType", "infantry" } };
        var evtVehicle = new BsonDocument { { "indirect", false }, { "targetType", "vehicle" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evtInfantry, stats);
        _subject.ProcessForPlayer(evtInfantry, stats);
        _subject.ProcessForPlayer(evtVehicle, stats);

        stats.KillsByTargetType.Should().ContainKey("infantry").WhoseValue.Should().Be(2);
        stats.KillsByTargetType.Should().ContainKey("vehicle").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ProcessForPlayer_WhenMissingFields_ShouldUseDefaults()
    {
        var evt = new BsonDocument();
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.Kills.Direct.Should().Be(1);
        stats.KillsByTargetType.Should().ContainKey("unknown");
    }

    [Fact]
    public void ProcessForPlayer_StructureKill_ShouldTrackAsStructure()
    {
        var evt = new BsonDocument { { "indirect", false }, { "targetType", "structure" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.Kills.Direct.Should().Be(1);
        stats.KillsByTargetType.Should().ContainKey("structure").WhoseValue.Should().Be(1);
    }
}
