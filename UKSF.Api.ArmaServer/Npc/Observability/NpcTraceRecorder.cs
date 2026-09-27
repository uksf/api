using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.Npc.Models;

namespace UKSF.Api.ArmaServer.Npc.Observability;

/// One captured event, already serialised so the writer does no work per field.
public sealed record NpcTraceQueued(string Session, long Seq, byte[] Bson);

public interface INpcTraceRecorder
{
    /// Never blocks and never throws. A full queue drops the event and records the gap.
    void Record(string type, string session, object data, string npc = null, string turn = null, string utt = null);
}

public sealed class NullNpcTraceRecorder : INpcTraceRecorder
{
    public static readonly NullNpcTraceRecorder Instance = new();
    public void Record(string type, string session, object data, string npc = null, string turn = null, string utt = null) { }
}

/// NPC observability capture. Callers on the player path serialise one small document and hand
/// it to a channel; NpcTraceWriter owns all Mongo I/O. Order within a process is (session, seq).
public sealed class NpcTraceRecorder : INpcTraceRecorder
{
    public const int SchemaVersion = 1;
    public const int MaxStringChars = 200_000;
    public const int MaxEventBytes = 4 * 1024 * 1024;

    private readonly Channel<NpcTraceQueued> _channel = Channel.CreateUnbounded<NpcTraceQueued>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, StrongBox<long>> _seq = new();
    private readonly ConcurrentDictionary<string, (long From, long To, int Count)> _gaps = new();
    private readonly long _maxQueueBytes;
    private long _queuedBytes;

    public NpcTraceRecorder(long maxQueueBytes = 64L * 1024 * 1024) => _maxQueueBytes = maxQueueBytes;

    public string ProcessId { get; } = Guid.NewGuid().ToString("N")[..12];
    public ChannelReader<NpcTraceQueued> Reader => _channel.Reader;
    public long QueuedBytes => Interlocked.Read(ref _queuedBytes);

    public void Record(string type, string session, object data, string npc = null, string turn = null, string utt = null)
    {
        if (string.IsNullOrEmpty(session)) return;
        var seq = Interlocked.Increment(ref _seq.GetOrAdd(session, _ => new StrongBox<long>()).Value);
        try
        {
            var bytes = Build(type, session, seq, data, npc, turn, utt);
            if (Interlocked.Add(ref _queuedBytes, bytes.Length) > _maxQueueBytes)
            {
                Interlocked.Add(ref _queuedBytes, -bytes.Length);
                MarkGap(session, seq, seq, 1);
                return;
            }

            _channel.Writer.TryWrite(new NpcTraceQueued(session, seq, bytes));
        }
        catch
        {
            MarkGap(session, seq, seq, 1);
        }
    }

    internal void Dequeued(NpcTraceQueued item) => Interlocked.Add(ref _queuedBytes, -item.Bson.Length);

    internal void MarkGap(string session, long from, long to, int count) =>
        _gaps.AddOrUpdate(session, (from, to, count), (_, g) => (Math.Min(g.From, from), Math.Max(g.To, to), g.Count + count));

    /// Queues one telemetry.gap per session with a pending range. Gap events skip the byte cap.
    internal void FlushGaps()
    {
        foreach (var session in _gaps.Keys)
        {
            if (!_gaps.TryRemove(session, out var gap)) continue;
            var seq = Interlocked.Increment(ref _seq.GetOrAdd(session, _ => new StrongBox<long>()).Value);
            var bytes = Build(
                "telemetry.gap",
                session,
                seq,
                new
                {
                    from = gap.From,
                    to = gap.To,
                    count = gap.Count
                },
                null,
                null,
                null
            );
            Interlocked.Add(ref _queuedBytes, bytes.Length);
            _channel.Writer.TryWrite(new NpcTraceQueued(session, seq, bytes));
        }
    }

    internal bool HasGaps => !_gaps.IsEmpty;

    private byte[] Build(string type, string session, long seq, object data, string npc, string turn, string utt)
    {
        var truncated = false;
        var doc = new BsonDocument
        {
            { "_id", $"{ProcessId}:{session}:{seq}" },
            { "v", SchemaVersion },
            { "type", type },
            { "at", DateTime.UtcNow },
            { "proc", ProcessId },
            { "session", session },
            { "seq", seq }
        };
        if (!string.IsNullOrEmpty(npc)) doc["npc"] = npc;
        if (!string.IsNullOrEmpty(turn)) doc["turn"] = turn;
        if (!string.IsNullOrEmpty(utt)) doc["utt"] = utt;
        if (data is not null)
        {
            var body = data as BsonDocument ?? BsonDocument.Parse(JsonSerializer.Serialize(data, NpcBrainJson.Options));
            doc["data"] = Cap(body, ref truncated);
        }

        if (truncated) doc["truncated"] = true;
        var bytes = doc.ToBson();
        if (bytes.Length <= MaxEventBytes) return bytes;

        doc["data"] = new BsonDocument("oversizeBytes", bytes.Length);
        doc["truncated"] = true;
        return doc.ToBson();
    }

    private static BsonValue Cap(BsonValue value, ref bool truncated)
    {
        switch (value)
        {
            case BsonString s when s.Value.Length > MaxStringChars:
                truncated = true;
                return new BsonString(s.Value[..MaxStringChars]);
            case BsonDocument d:
                foreach (var element in d.ToList()) d[element.Name] = Cap(element.Value, ref truncated);
                return d;
            case BsonArray a:
                for (var i = 0; i < a.Count; i++) a[i] = Cap(a[i], ref truncated);
                return a;
            default: return value;
        }
    }
}
