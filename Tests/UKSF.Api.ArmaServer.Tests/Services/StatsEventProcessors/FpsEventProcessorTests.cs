using FluentAssertions;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services.StatsEventProcessors;

public class FpsEventProcessorTests
{
    private readonly FpsEventProcessor _subject = new();

    [Fact]
    public void EventType_ShouldBeFps()
    {
        _subject.EventType.Should().Be("fps");
    }

    [Fact]
    public void ProcessForPlayer_ShouldBeNoOp()
    {
        var evt = new BsonDocument { { "value", 60 } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt, stats);

        // FPS stats are computed on mission end, not per-event
        stats.FpsMin.Should().BeNull();
        stats.FpsMax.Should().BeNull();
        stats.FpsAverage.Should().BeNull();
        stats.FpsP1.Should().BeNull();
    }
}
