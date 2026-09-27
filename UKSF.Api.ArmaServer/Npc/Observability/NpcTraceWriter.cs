using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Npc.Observability;

public interface INpcTraceSink
{
    /// Inserts one batch. Returns the queued items that failed permanently.
    Task<IReadOnlyList<NpcTraceQueued>> InsertAsync(IReadOnlyList<NpcTraceQueued> batch, CancellationToken cancellationToken);
}

public sealed class MongoNpcTraceSink(IMongoDatabase database) : INpcTraceSink
{
    public const string Collection = "npcObservabilityEvents";
    private readonly IMongoCollection<RawBsonDocument> _events = database.GetCollection<RawBsonDocument>(Collection);

    public async Task<IReadOnlyList<NpcTraceQueued>> InsertAsync(IReadOnlyList<NpcTraceQueued> batch, CancellationToken cancellationToken)
    {
        try
        {
            await _events.InsertManyAsync(batch.Select(x => new RawBsonDocument(x.Bson)), new InsertManyOptions { IsOrdered = false }, cancellationToken);
            return [];
        }
        catch (MongoBulkWriteException<RawBsonDocument> exception) when (exception.WriteConcernError is null)
        {
            // A duplicate key is a retried insert that already landed.
            return exception.WriteErrors.Where(e => e.Category != ServerErrorCategory.DuplicateKey).Select(e => batch[e.Index]).ToList();
        }
    }
}

/// Owns all NPC trace I/O. Batches whatever is queued, retries transient failures briefly, and
/// turns anything it cannot write into a telemetry.gap. On API stop it drains for at most 2 s.
public sealed class NpcTraceWriter(NpcTraceRecorder recorder, INpcTraceSink sink, IUksfLogger logger) : BackgroundService
{
    public const int BatchSize = 200;
    public static readonly TimeSpan DrainLimit = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await recorder.Reader.WaitToReadAsync(stoppingToken))
            {
                await WriteBatchAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }

        using var drain = new CancellationTokenSource(DrainLimit);
        try
        {
            while (!drain.IsCancellationRequested && recorder.Reader.TryPeek(out _))
            {
                await WriteBatchAsync(drain.Token);
            }
        }
        catch (OperationCanceledException) { }
    }

    internal async Task WriteBatchAsync(CancellationToken cancellationToken)
    {
        var batch = new List<NpcTraceQueued>(BatchSize);
        while (batch.Count < BatchSize && recorder.Reader.TryRead(out var item))
        {
            recorder.Dequeued(item);
            batch.Add(item);
        }

        if (batch.Count == 0) return;

        IReadOnlyList<NpcTraceQueued> failed = batch;
        for (var attempt = 0; attempt <= Backoff.Length && failed.Count > 0; attempt++)
        {
            try
            {
                var permanent = await sink.InsertAsync(failed, cancellationToken);
                if (permanent.Count > 0) Gap(permanent, "write rejected");
                failed = [];
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (attempt == Backoff.Length)
                {
                    logger.LogError($"npc trace: batch of {failed.Count} failed after retries", exception);
                    break;
                }

                await Task.Delay(Backoff[attempt], cancellationToken);
            }
        }

        if (failed.Count > 0) Gap(failed, "retries exhausted");
        if (recorder.HasGaps) recorder.FlushGaps();
    }

    private void Gap(IReadOnlyList<NpcTraceQueued> items, string reason)
    {
        logger.LogWarning($"npc trace: {items.Count} events lost ({reason})");
        // A lost gap record is only logged; re-queuing it could loop forever.
        foreach (var session in items.Where(x => !x.IsGap).GroupBy(x => x.Session))
        {
            recorder.MarkGap(session.Key, session.Min(x => x.Seq), session.Max(x => x.Seq), session.Count());
        }
    }
}
