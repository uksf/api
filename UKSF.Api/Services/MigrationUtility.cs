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
    // When bumping Version and adding a new MigrateXxx step, delete the migrations from the previous version —
    // they have already run on every active db (the version marker prevents them re-running) and operate on
    // collections/fields that may no longer exist. Keep only the migration(s) introduced for the current Version.
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
        await MigrateStatsBatchesSplit();
        logger.LogInfo("All migrations completed successfully");
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
