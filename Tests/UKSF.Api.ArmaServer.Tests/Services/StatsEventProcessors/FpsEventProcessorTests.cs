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
    public void ProcessForPlayer_ShouldTrackSampleCountAndSum()
    {
        var evt1 = new BsonDocument { { "value", 60 } };
        var evt2 = new BsonDocument { { "value", 45 } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);

        stats.FpsSampleCount.Should().Be(2);
        stats.FpsTotalSum.Should().Be(105);
    }

    [Fact]
    public void ProcessForPlayer_ShouldTrackMinFps()
    {
        var evt1 = new BsonDocument { { "value", 60 } };
        var evt2 = new BsonDocument { { "value", 25 } };
        var evt3 = new BsonDocument { { "value", 45 } };
        var stats = new PlayerMissionStats();

        _subject.ProcessForPlayer(evt1, stats);
        _subject.ProcessForPlayer(evt2, stats);
        _subject.ProcessForPlayer(evt3, stats);

        stats.FpsMin.Should().Be(25);
    }
}
