using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Moq;
using UKSF.Api.ArmaServer.Npc.Observability;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Npc.Observability;

public class NpcTraceWriterTests
{
    private sealed class FakeSink : INpcTraceSink
    {
        public int FailTimes { get; set; }
        public Func<IReadOnlyList<NpcTraceQueued>, IReadOnlyList<NpcTraceQueued>> Permanent { get; set; } = _ => [];
        public List<BsonDocument> Written { get; } = [];
        public TaskCompletionSource FirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }

        public Task<IReadOnlyList<NpcTraceQueued>> InsertAsync(IReadOnlyList<NpcTraceQueued> batch, CancellationToken cancellationToken)
        {
            Calls++;
            if (FailTimes-- > 0) throw new TimeoutException("mongo slow");
            var permanent = Permanent(batch);
            Written.AddRange(batch.Except(permanent).Select(x => BsonSerializer.Deserialize<BsonDocument>(x.Bson)));
            FirstWrite.TrySetResult();
            return Task.FromResult(permanent);
        }
    }

    [Fact]
    public async Task WriteBatch_InsertsQueuedEvents()
    {
        var recorder = new NpcTraceRecorder();
        var sink = new FakeSink();
        var writer = new NpcTraceWriter(recorder, sink, Mock.Of<IUksfLogger>());
        recorder.Record("a", "s1", null);
        recorder.Record("b", "s1", null);

        await writer.WriteBatchAsync(CancellationToken.None);

        sink.Written.Select(x => x["type"].AsString).Should().Equal("a", "b");
        recorder.QueuedBytes.Should().Be(0);
    }

    [Fact]
    public async Task WriteBatch_RetriesTransientFailure()
    {
        var recorder = new NpcTraceRecorder();
        var sink = new FakeSink { FailTimes = 1 };
        var writer = new NpcTraceWriter(recorder, sink, Mock.Of<IUksfLogger>());
        recorder.Record("a", "s1", null);

        await writer.WriteBatchAsync(CancellationToken.None);

        sink.Calls.Should().Be(2);
        sink.Written.Should().ContainSingle();
        recorder.HasGaps.Should().BeFalse();
    }

    [Fact]
    public async Task WriteBatch_PermanentRejection_BecomesGap()
    {
        var recorder = new NpcTraceRecorder();
        var sink = new FakeSink { Permanent = batch => batch.Where(x => x.Seq == 2).ToList() };
        var writer = new NpcTraceWriter(recorder, sink, Mock.Of<IUksfLogger>());
        recorder.Record("a", "s1", null);
        recorder.Record("bad", "s1", null);
        recorder.Record("c", "s1", null);

        await writer.WriteBatchAsync(CancellationToken.None);
        await writer.WriteBatchAsync(CancellationToken.None);

        var gap = sink.Written.Single(x => x["type"] == "telemetry.gap");
        gap["data"]["from"].ToInt64().Should().Be(2);
        gap["data"]["to"].ToInt64().Should().Be(2);
        sink.Written.Select(x => x["type"].AsString).Should().Contain(["a", "c"]);
    }

    [Fact]
    public async Task Execute_OnStop_DrainsWithinLimit()
    {
        var recorder = new NpcTraceRecorder();
        var sink = new FakeSink();
        var writer = new NpcTraceWriter(recorder, sink, Mock.Of<IUksfLogger>());
        await writer.StartAsync(CancellationToken.None);
        // The host runs ExecuteAsync on the thread pool; wait until it is live so stop reaches the drain.
        recorder.Record("warm", "s1", null);
        (await Task.WhenAny(sink.FirstWrite.Task, Task.Delay(10_000))).Should().BeSameAs(sink.FirstWrite.Task);
        for (var i = 0; i < 500; i++) recorder.Record("a", "s1", null);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await writer.StopAsync(CancellationToken.None);

        watch.Elapsed.Should().BeLessThan(NpcTraceWriter.DrainLimit + TimeSpan.FromSeconds(1));
        sink.Written.Should().HaveCount(501);
    }

    [Fact]
    public async Task WriteBatch_LostGapEvent_IsNotRequeuedAsAnotherGap()
    {
        var recorder = new NpcTraceRecorder();
        var sink = new FakeSink { Permanent = batch => batch.ToList() };
        var writer = new NpcTraceWriter(recorder, sink, Mock.Of<IUksfLogger>());
        recorder.Record("a", "s1", null);

        await writer.WriteBatchAsync(CancellationToken.None);
        await writer.WriteBatchAsync(CancellationToken.None);

        recorder.HasGaps.Should().BeFalse();
        recorder.Reader.TryPeek(out _).Should().BeFalse();
    }
}
