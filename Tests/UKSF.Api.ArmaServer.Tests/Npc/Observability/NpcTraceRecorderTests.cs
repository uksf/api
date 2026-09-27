using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using UKSF.Api.ArmaServer.Npc.Observability;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc.Observability;

public class NpcTraceRecorderTests
{
    private static List<BsonDocument> Drain(NpcTraceRecorder recorder)
    {
        var docs = new List<BsonDocument>();
        while (recorder.Reader.TryRead(out var item))
        {
            recorder.Dequeued(item);
            docs.Add(BsonSerializer.Deserialize<BsonDocument>(item.Bson));
        }

        return docs;
    }

    [Fact]
    public void Record_WritesCompactEnvelope_AndOmitsNulls()
    {
        var recorder = new NpcTraceRecorder();

        recorder.Record(
            "turn.decided",
            "s1",
            new
            {
                address = "answer",
                decision = (string)null,
                calls = new[] { new { ms = 5 } }
            },
            "npc1",
            "t1"
        );

        var doc = Drain(recorder).Should().ContainSingle().Subject;
        doc["_id"].AsString.Should().Be($"{recorder.ProcessId}:s1:1");
        doc["type"].AsString.Should().Be("turn.decided");
        doc["seq"].AsInt64.Should().Be(1);
        doc["npc"].AsString.Should().Be("npc1");
        doc["turn"].AsString.Should().Be("t1");
        doc.Contains("utt").Should().BeFalse();
        doc["data"]["address"].AsString.Should().Be("answer");
        doc["data"].AsBsonDocument.Contains("decision").Should().BeFalse();
        doc.Contains("truncated").Should().BeFalse();
    }

    [Fact]
    public void Record_NumbersSequencePerSession()
    {
        var recorder = new NpcTraceRecorder();

        recorder.Record("a", "s1", null);
        recorder.Record("a", "s2", null);
        recorder.Record("a", "s1", null);

        Drain(recorder).Select(d => $"{d["session"]}:{d["seq"]}").Should().Equal("s1:1", "s2:1", "s1:2");
    }

    [Fact]
    public void Record_CapsLongStrings_AndMarksTruncated()
    {
        var recorder = new NpcTraceRecorder();

        recorder.Record("turn.replied", "s1", new { request = new string('x', NpcTraceRecorder.MaxStringChars + 10) });

        var doc = Drain(recorder).Single();
        doc["data"]["request"].AsString.Length.Should().Be(NpcTraceRecorder.MaxStringChars);
        doc["truncated"].AsBoolean.Should().BeTrue();
    }

    [Fact]
    public void Record_WhenQueueFull_DropsWithoutBlocking_AndGapCoversDroppedRange()
    {
        var recorder = new NpcTraceRecorder(maxQueueBytes: 400);
        var watch = Stopwatch.StartNew();

        for (var i = 0; i < 10_000; i++) recorder.Record("utterance.received", "s1", new { text = "hello there" });

        watch.ElapsedMilliseconds.Should().BeLessThan(2000);
        var kept = Drain(recorder);
        kept.Should().NotBeEmpty().And.HaveCountLessThan(10_000);
        recorder.HasGaps.Should().BeTrue();

        recorder.FlushGaps();
        var gap = Drain(recorder).Single();
        gap["type"].AsString.Should().Be("telemetry.gap");
        gap["data"]["from"].ToInt64().Should().Be(kept.Count + 1);
        gap["data"]["to"].ToInt64().Should().Be(10_000);
        gap["data"]["count"].ToInt32().Should().Be(10_000 - kept.Count);
        gap["seq"].AsInt64.Should().Be(10_001);
    }

    [Fact]
    public void Record_IgnoresMissingSession()
    {
        var recorder = new NpcTraceRecorder();

        recorder.Record("a", "", new { x = 1 });

        Drain(recorder).Should().BeEmpty();
    }

    [Fact]
    public void Record_WhenQueueAlreadyFull_SkipsSerialisation()
    {
        var recorder = new NpcTraceRecorder(maxQueueBytes: 1);
        recorder.Record("a", "s1", null);
        var poison = new ThrowingOnSerialise();

        recorder.Record("b", "s1", poison);

        poison.Touched.Should().BeFalse();
    }

    private sealed class ThrowingOnSerialise
    {
        public bool Touched { get; private set; }

        public string Value
        {
            get
            {
                Touched = true;
                return "x";
            }
        }
    }
}
