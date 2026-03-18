using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services.StatsEventProcessors;

public class DistanceOnFootEventProcessorTests
{
    private readonly DistanceOnFootEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeDistanceOnFoot()
    {
        _subject.EventType.Should().Be("distanceOnFoot");
    }

    [Fact]
    public void ProcessForPlayer_ShouldAccumulateDistance()
    {
        var evt1 = new BsonDocument { { "metres", 500 } };
        var evt2 = new BsonDocument { { "metres", 300 } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        stats.DistanceOnFoot.Should().Be(800);
    }
}

public class DistanceInVehicleEventProcessorTests
{
    private readonly DistanceInVehicleEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeDistanceInVehicle()
    {
        _subject.EventType.Should().Be("distanceInVehicle");
    }

    [Fact]
    public void ProcessForPlayer_ShouldAccumulateDistance()
    {
        var evt1 = new BsonDocument { { "metres", 2000 } };
        var evt2 = new BsonDocument { { "metres", 1500 } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        stats.DistanceInVehicle.Should().Be(3500);
    }
}

public class FuelConsumedEventProcessorTests
{
    private readonly FuelConsumedEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeFuelConsumed()
    {
        _subject.EventType.Should().Be("fuelConsumed");
    }

    [Fact]
    public void ProcessForPlayer_ShouldAccumulateFuel()
    {
        var evt1 = new BsonDocument { { "amount", 0.15 } };
        var evt2 = new BsonDocument { { "amount", 0.08 } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        stats.TotalFuelConsumed.Should().BeApproximately(0.23, 0.001);
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
