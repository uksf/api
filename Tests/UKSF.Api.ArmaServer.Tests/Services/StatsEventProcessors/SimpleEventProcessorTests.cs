using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services.StatsEventProcessors;

public class SamplerBatchEventProcessorTests
{
    private readonly SamplerBatchEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeSamplerBatch()
    {
        _subject.EventType.Should().Be("samplerBatch");
    }

    [Fact]
    public void ProcessForPlayer_ShouldSumPositiveEntriesAndIgnoreGaps()
    {
        var evt = new BsonDocument
        {
            {
                "distanceOnFoot", new BsonArray
                {
                    12,
                    8,
                    -3,
                    5,
                    -2,
                    7,
                    -1
                }
            },
            {
                "distanceInVehicle", new BsonArray
                {
                    1500,
                    -2,
                    2000
                }
            },
            {
                "fuelLitres", new BsonArray
                {
                    4,
                    -1,
                    6
                }
            }
        };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.DistanceOnFoot.Should().Be(32);
        stats.DistanceInVehicle.Should().Be(3500);
        stats.TotalFuelLitres.Should().Be(10);
    }

    [Fact]
    public void ProcessForPlayer_ShouldAccumulateAcrossEvents()
    {
        var stats = new PlayerMissionStats();
        var evt1 = new BsonDocument { { "distanceOnFoot", new BsonArray { 100, 50 } } };
        var evt2 = new BsonDocument { { "distanceOnFoot", new BsonArray { 25 } } };

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        stats.DistanceOnFoot.Should().Be(175);
    }

    [Fact]
    public void ProcessForPlayer_ShouldHandleMissingFields()
    {
        var evt = new BsonDocument();
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        stats.DistanceOnFoot.Should().Be(0);
        stats.DistanceInVehicle.Should().Be(0);
        stats.TotalFuelLitres.Should().Be(0);
    }
}

public class ExplosivePlacedEventProcessorTests
{
    private readonly ExplosivePlacedEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeExplosivePlaced()
    {
        _subject.EventType.Should().Be("explosivePlaced");
    }

    [Fact]
    public void ProcessForPlayer_ShouldIncrementCount()
    {
        var evt = new BsonDocument { { "explosiveClassname", "DemoCharge_Remote_Mag" } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);
        _subject.ProcessForPlayer(evt, stats);

        stats.ExplosivesPlaced.Should().Be(2);
    }
}

public class UnconsciousEventProcessorTests
{
    private readonly UnconsciousEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeUnconscious()
    {
        _subject.EventType.Should().Be("unconscious");
    }

    [Fact]
    public void ProcessForPlayer_ShouldIncrementCount()
    {
        var evt = new BsonDocument();
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);
        _subject.ProcessForPlayer(evt, stats);
        _subject.ProcessForPlayer(evt, stats);

        stats.TimesUnconscious.Should().Be(3);
    }
}
