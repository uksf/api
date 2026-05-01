using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IMongoDatabase database, IUksfLogger logger)
{
    private const int Version = 11;

    public async Task RunMigrations()
    {
        if (migrationContext.GetSingle(x => x.Version == Version) is not null)
        {
            return;
        }

        try
        {
            await ExecuteMigrations();
            await migrationContext.Add(new Migration { Version = Version });
            logger.LogInfo($"Migration version {Version} executed successfully");
        }
        catch (Exception e)
        {
            logger.LogError(e);
            throw;
        }
    }

    private async Task ExecuteMigrations()
    {
        await MigrateStatisticsSessionIds();
        await MigrateStatisticsCleanup();
        await MigrateStatisticsComputeFps();
        await MigrateStatsBatchesSplit();
        logger.LogInfo("All migrations completed successfully");
    }

    private async Task MigrateStatisticsSessionIds()
    {
        logger.LogInfo("Starting migration to fix statistics session ID references");

        var sessionsCollection = database.GetCollection<BsonDocument>("missionSessions");
        var missionStatsCollection = database.GetCollection<BsonDocument>("missionStats");
        var playerStatsCollection = database.GetCollection<BsonDocument>("playerMissionStats");

        // Build lookup: ObjectId string -> UUID sessionId
        var sessions = await sessionsCollection.Find(new BsonDocument()).ToListAsync();
        var idToSessionId = new Dictionary<string, string>();
        foreach (var session in sessions)
        {
            var objectIdStr = session["_id"].AsObjectId.ToString();
            var sessionId = session.GetValue("sessionId", "").AsString;
            if (!string.IsNullOrEmpty(sessionId))
            {
                idToSessionId[objectIdStr] = sessionId;
            }
        }

        // Fix missionStats
        var allMissionStats = await missionStatsCollection.Find(new BsonDocument()).ToListAsync();
        var missionStatsFixed = 0;
        foreach (var doc in allMissionStats)
        {
            var currentId = doc.GetValue("missionSessionId", "").AsString;
            if (idToSessionId.TryGetValue(currentId, out var uuid))
            {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
                var update = Builders<BsonDocument>.Update.Set("missionSessionId", uuid);
                await missionStatsCollection.UpdateOneAsync(filter, update);
                missionStatsFixed++;
            }
        }

        // Fix playerMissionStats
        var allPlayerStats = await playerStatsCollection.Find(new BsonDocument()).ToListAsync();
        var playerStatsFixed = 0;
        foreach (var doc in allPlayerStats)
        {
            var currentId = doc.GetValue("missionSessionId", "").AsString;
            if (idToSessionId.TryGetValue(currentId, out var uuid))
            {
                var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
                var update = Builders<BsonDocument>.Update.Set("missionSessionId", uuid);
                await playerStatsCollection.UpdateOneAsync(filter, update);
                playerStatsFixed++;
            }
        }

        logger.LogInfo($"Fixed {missionStatsFixed} missionStats and {playerStatsFixed} playerMissionStats session ID references");
    }

    private async Task MigrateStatisticsCleanup()
    {
        logger.LogInfo("Starting migration to clean up statistics fields and merge batches");

        // Remove dead fields from missionSessions
        var sessionsCollection = database.GetCollection<BsonDocument>("missionSessions");
        var unsetResult = await sessionsCollection.UpdateManyAsync(new BsonDocument(), Builders<BsonDocument>.Update.Unset("date").Unset("type"));
        logger.LogInfo($"Removed date/type from {unsetResult.ModifiedCount} missionSessions documents");

        // Remove old FPS fields from playerMissionStats
        var playerStatsCollection = database.GetCollection<BsonDocument>("playerMissionStats");
        var fpsResult = await playerStatsCollection.UpdateManyAsync(
            new BsonDocument(),
            Builders<BsonDocument>.Update.Unset("fpsSampleCount").Unset("fpsTotalSum").Unset("fpsMin")
        );
        logger.LogInfo($"Removed old FPS fields from {fpsResult.ModifiedCount} playerMissionStats documents");

        // Merge batches per session
        var batchesCollection = database.GetCollection<BsonDocument>("missionStatsBatches");
        var distinctSessionIds = await batchesCollection.DistinctAsync<string>("missionSessionId", new BsonDocument());
        var sessionIds = await distinctSessionIds.ToListAsync();
        var mergedCount = 0;

        foreach (var sessionId in sessionIds)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("missionSessionId", sessionId);
            var batches = await batchesCollection.Find(filter).SortBy(b => b["receivedAt"]).ToListAsync();

            if (batches.Count <= 1)
            {
                continue;
            }

            var mergedEvents = new BsonArray();
            foreach (var batch in batches)
            {
                if (batch.Contains("events") && batch["events"].IsBsonArray)
                {
                    mergedEvents.AddRange(batch["events"].AsBsonArray);
                }
            }

            var mergedBatch = new BsonDocument
            {
                { "missionSessionId", sessionId },
                { "mission", batches[0].GetValue("mission", "") },
                { "map", batches[0].GetValue("map", "") },
                { "receivedAt", batches[0].GetValue("receivedAt", BsonNull.Value) },
                { "events", mergedEvents }
            };

            await batchesCollection.InsertOneAsync(mergedBatch);
            await batchesCollection.DeleteManyAsync(Builders<BsonDocument>.Filter.In("_id", batches.Select(b => b["_id"])));
            mergedCount++;
        }

        logger.LogInfo($"Merged batches for {mergedCount} sessions");
    }

    private async Task MigrateStatisticsComputeFps()
    {
        logger.LogInfo("Starting migration to compute FPS stats from historical batch data");

        var batchesCollection = database.GetCollection<BsonDocument>("missionStatsBatches");
        var playerStatsCollection = database.GetCollection<BsonDocument>("playerMissionStats");

        var distinctSessionIds = await batchesCollection.DistinctAsync<string>("missionSessionId", new BsonDocument());
        var sessionIds = await distinctSessionIds.ToListAsync();
        var playersUpdated = 0;

        foreach (var sessionId in sessionIds)
        {
            var batches = await batchesCollection.Find(Builders<BsonDocument>.Filter.Eq("missionSessionId", sessionId)).ToListAsync();

            var fpsByPlayer = new Dictionary<string, List<int>>();
            foreach (var batch in batches)
            {
                if (!batch.Contains("events") || !batch["events"].IsBsonArray)
                {
                    continue;
                }

                foreach (var evt in batch["events"].AsBsonArray)
                {
                    if (!evt.IsBsonDocument)
                    {
                        continue;
                    }

                    var doc = evt.AsBsonDocument;
                    if (!doc.Contains("type") || doc["type"].AsString != "fps" || !doc.Contains("uid") || !doc.Contains("value"))
                    {
                        continue;
                    }

                    var uid = doc["uid"].AsString;
                    var value = doc["value"].ToInt32();

                    if (!fpsByPlayer.TryGetValue(uid, out var samples))
                    {
                        samples = [];
                        fpsByPlayer[uid] = samples;
                    }

                    samples.Add(value);
                }
            }

            foreach (var (uid, samples) in fpsByPlayer)
            {
                if (samples.Count == 0)
                {
                    continue;
                }

                samples.Sort();
                var min = samples[0];
                var max = samples[^1];
                var average = samples.Average();
                var p1Index = Math.Max(0, (int)Math.Ceiling(samples.Count * 0.01) - 1);
                var p1 = samples[p1Index];

                var filter = Builders<BsonDocument>.Filter.Eq("missionSessionId", sessionId) & Builders<BsonDocument>.Filter.Eq("playerUid", uid);
                var update = Builders<BsonDocument>.Update.Set("fpsMin", min).Set("fpsMax", max).Set("fpsAverage", average).Set("fpsP1", p1);
                var result = await playerStatsCollection.UpdateOneAsync(filter, update);
                if (result.ModifiedCount > 0)
                {
                    playersUpdated++;
                }
            }
        }

        logger.LogInfo($"Computed FPS stats for {playersUpdated} player-session records");
    }

    private async Task MigrateStatsBatchesSplit()
    {
        logger.LogInfo("Starting migration to split legacy missionStatsBatches into sampler/combat/lifecycle collections");

        var batchesCollection = database.GetCollection<BsonDocument>("missionStatsBatches");
        var samplerCollection = database.GetCollection<BsonDocument>("missionStatsEventsSampler");
        var combatCollection = database.GetCollection<BsonDocument>("missionStatsEventsCombat");
        var lifecycleCollection = database.GetCollection<BsonDocument>("missionStatsEventsLifecycle");

        var distinctSessionIds = await batchesCollection.DistinctAsync<string>("missionSessionId", new BsonDocument());
        var sessionIds = await distinctSessionIds.ToListAsync();

        var totalEvents = 0;
        var sessionsMigrated = 0;
        var failedSessions = new List<string>();

        foreach (var sessionId in sessionIds)
        {
            try
            {
                var batches = await batchesCollection.Find(Builders<BsonDocument>.Filter.Eq("missionSessionId", sessionId))
                                                     .Sort(Builders<BsonDocument>.Sort.Ascending("receivedAt"))
                                                     .ToListAsync();

                var allEvents = new List<BsonDocument>();
                foreach (var batch in batches)
                {
                    if (!batch.Contains("events") || !batch["events"].IsBsonArray)
                    {
                        continue;
                    }

                    foreach (var evt in batch["events"].AsBsonArray)
                    {
                        if (evt.IsBsonDocument)
                        {
                            allEvents.Add(evt.AsBsonDocument);
                        }
                    }
                }

                totalEvents += allEvents.Count;
                sessionsMigrated++;

                var split = RawEventSplitter.Split(allEvents);

                foreach (var (uid, samplerEvents) in split.SamplerByUid)
                {
                    var existing = await samplerCollection.Find(
                                                              Builders<BsonDocument>.Filter.Eq("missionSessionId", sessionId) &
                                                              Builders<BsonDocument>.Filter.Eq("playerUid", uid)
                                                          )
                                                          .FirstOrDefaultAsync();
                    if (existing is not null)
                    {
                        continue;
                    }

                    var distanceOnFoot = new BsonArray();
                    var distanceInVehicle = new BsonArray();
                    var fuelLitres = new BsonArray();
                    var firstTs = DateTime.MaxValue;
                    var lastTs = DateTime.MinValue;

                    foreach (var evt in samplerEvents)
                    {
                        AppendNumericSeries(distanceOnFoot, evt, "distanceOnFoot");
                        AppendNumericSeries(distanceInVehicle, evt, "distanceInVehicle");
                        AppendNumericSeries(fuelLitres, evt, "fuelLitres");

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

                    await samplerCollection.InsertOneAsync(
                        new BsonDocument
                        {
                            { "_id", ObjectId.GenerateNewId() },
                            { "missionSessionId", sessionId },
                            { "playerUid", uid },
                            { "firstTimestamp", firstStamp },
                            { "lastTimestamp", lastStamp },
                            { "distanceOnFoot", distanceOnFoot },
                            { "distanceInVehicle", distanceInVehicle },
                            { "fuelLitres", fuelLitres }
                        }
                    );
                }

                if (split.Combat.Count > 0)
                {
                    var combatExists = await combatCollection.Find(Builders<BsonDocument>.Filter.Eq("missionSessionId", sessionId)).AnyAsync();
                    if (!combatExists)
                    {
                        var bucketIndex = 1;
                        for (var i = 0; i < split.Combat.Count; i += MissionStatsEventsCombat.MaxEventsPerBucket)
                        {
                            var slice = split.Combat.Skip(i).Take(MissionStatsEventsCombat.MaxEventsPerBucket).ToList();
                            var eventsArray = new BsonArray();
                            foreach (var evt in slice) eventsArray.Add(evt);

                            await combatCollection.InsertOneAsync(
                                new BsonDocument
                                {
                                    { "_id", ObjectId.GenerateNewId() },
                                    { "missionSessionId", sessionId },
                                    { "bucketIndex", bucketIndex },
                                    { "eventCount", slice.Count },
                                    { "events", eventsArray }
                                }
                            );
                            bucketIndex++;
                        }
                    }
                }

                if (split.Lifecycle.Count > 0)
                {
                    var lifecycleExists = await lifecycleCollection.Find(Builders<BsonDocument>.Filter.Eq("missionSessionId", sessionId)).AnyAsync();
                    if (!lifecycleExists)
                    {
                        var eventsArray = new BsonArray();
                        foreach (var evt in split.Lifecycle) eventsArray.Add(evt);

                        await lifecycleCollection.InsertOneAsync(
                            new BsonDocument
                            {
                                { "_id", ObjectId.GenerateNewId() },
                                { "missionSessionId", sessionId },
                                { "events", eventsArray }
                            }
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Migration_StatsBatchesSplit failed for session {sessionId}", ex);
                failedSessions.Add(sessionId);
            }
        }

        if (failedSessions.Count > 0)
        {
            throw new InvalidOperationException($"Migration_StatsBatchesSplit failed for {failedSessions.Count} sessions: {string.Join(", ", failedSessions)}");
        }

        await database.DropCollectionAsync("missionStatsBatches");

        logger.LogInfo($"Split {sessionsMigrated} sessions ({totalEvents} events) into sampler/combat/lifecycle collections");
    }

    private static void AppendNumericSeries(BsonArray target, BsonDocument evt, string field)
    {
        if (!evt.TryGetValue(field, out var value) || !value.IsBsonArray)
        {
            return;
        }

        foreach (var entry in value.AsBsonArray)
        {
            if (entry.IsNumeric) target.Add(entry.ToDouble());
        }
    }
}
