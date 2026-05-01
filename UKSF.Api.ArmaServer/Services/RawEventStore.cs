using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IRawEventStore
{
    Task StoreAsync(string sessionId, List<BsonDocument> events);
}

public class RawEventStore(
    IMissionStatsEventsSamplerContext samplerContext,
    IMissionStatsEventsCombatContext combatContext,
    IMissionStatsEventsLifecycleContext lifecycleContext
) : IRawEventStore
{
    public async Task StoreAsync(string sessionId, List<BsonDocument> events)
    {
        var split = RawEventSplitter.Split(events);

        if (split.SamplerByUid.Count > 0)
        {
            var samplerTasks = split.SamplerByUid.Select(kvp => UpsertSamplerAsync(sessionId, kvp.Key, kvp.Value));
            await Task.WhenAll(samplerTasks);
        }

        if (split.Combat.Count > 0)
        {
            await AppendCombatAsync(sessionId, split.Combat);
        }

        if (split.Lifecycle.Count > 0)
        {
            await AppendLifecycleAsync(sessionId, split.Lifecycle);
        }
    }

    private async Task UpsertSamplerAsync(string sessionId, string uid, List<BsonDocument> events)
    {
        var distanceOnFoot = new List<double>();
        var distanceInVehicle = new List<double>();
        var fuelLitres = new List<double>();
        var firstTs = DateTime.MaxValue;
        var lastTs = DateTime.MinValue;

        foreach (var evt in events)
        {
            distanceOnFoot.AddRange(ReadSeries(evt, "distanceOnFoot"));
            distanceInVehicle.AddRange(ReadSeries(evt, "distanceInVehicle"));
            fuelLitres.AddRange(ReadSeries(evt, "fuelLitres"));

            if (evt.TryGetValue("timestamp", out var ts) &&
                ts.IsString &&
                DateTime.TryParse(ts.AsString, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                if (parsed < firstTs) firstTs = parsed;
                if (parsed > lastTs) lastTs = parsed;
            }
        }

        var firstStamp = firstTs == DateTime.MaxValue ? DateTime.UtcNow : firstTs;
        var lastStamp = lastTs == DateTime.MinValue ? DateTime.UtcNow : lastTs;

        var update = Builders<MissionStatsEventsSampler>.Update.SetOnInsert(x => x.MissionSessionId, sessionId)
                                                        .SetOnInsert(x => x.PlayerUid, uid)
                                                        .Min(x => x.FirstTimestamp, firstStamp)
                                                        .Max(x => x.LastTimestamp, lastStamp)
                                                        .PushEach(x => x.DistanceOnFoot, distanceOnFoot)
                                                        .PushEach(x => x.DistanceInVehicle, distanceInVehicle)
                                                        .PushEach(x => x.FuelLitres, fuelLitres);

        await samplerContext.Upsert(x => x.MissionSessionId == sessionId && x.PlayerUid == uid, update);
    }

    private static IEnumerable<double> ReadSeries(BsonDocument evt, string field)
    {
        if (!evt.TryGetValue(field, out var value) || !value.IsBsonArray)
        {
            yield break;
        }

        foreach (var entry in value.AsBsonArray)
        {
            if (entry.IsNumeric) yield return entry.ToDouble();
        }
    }

    private async Task AppendCombatAsync(string sessionId, List<BsonDocument> events)
    {
        var existingTopBucket = combatContext.Get(b => b.MissionSessionId == sessionId).OrderByDescending(b => b.BucketIndex).FirstOrDefault();

        var nextBucketIndex = existingTopBucket?.BucketIndex ?? 0;
        var queue = new Queue<BsonDocument>(events);

        if (existingTopBucket is not null)
        {
            var roomLeft = MissionStatsEventsCombat.MaxEventsPerBucket - existingTopBucket.EventCount;
            if (roomLeft > 0)
            {
                var topUp = new List<BsonDocument>();
                while (topUp.Count < roomLeft && queue.Count > 0) topUp.Add(queue.Dequeue());

                if (topUp.Count > 0)
                {
                    var update = Builders<MissionStatsEventsCombat>.Update.PushEach(x => x.Events, topUp).Inc(x => x.EventCount, topUp.Count);
                    await combatContext.Update(existingTopBucket.Id, update);
                }
            }
        }

        while (queue.Count > 0)
        {
            var chunk = new List<BsonDocument>();
            while (chunk.Count < MissionStatsEventsCombat.MaxEventsPerBucket && queue.Count > 0) chunk.Add(queue.Dequeue());
            nextBucketIndex = await AddBucketWithRetryAsync(sessionId, nextBucketIndex, chunk);
        }
    }

    private async Task<int> AddBucketWithRetryAsync(string sessionId, int nextBucketIndex, List<BsonDocument> chunk)
    {
        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            nextBucketIndex++;
            try
            {
                await combatContext.Add(
                    new MissionStatsEventsCombat
                    {
                        MissionSessionId = sessionId,
                        BucketIndex = nextBucketIndex,
                        EventCount = chunk.Count,
                        Events = chunk
                    }
                );
                return nextBucketIndex;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                var latest = combatContext.Get(b => b.MissionSessionId == sessionId).OrderByDescending(b => b.BucketIndex).FirstOrDefault();
                nextBucketIndex = latest?.BucketIndex ?? 0;
            }
        }

        throw new InvalidOperationException($"Failed to allocate combat bucket for session '{sessionId}' after {maxAttempts} attempts");
    }

    private async Task AppendLifecycleAsync(string sessionId, List<BsonDocument> events)
    {
        var update = Builders<MissionStatsEventsLifecycle>.Update.SetOnInsert(x => x.MissionSessionId, sessionId).PushEach(x => x.Events, events);

        await lifecycleContext.Upsert(x => x.MissionSessionId == sessionId, update);
    }
}
