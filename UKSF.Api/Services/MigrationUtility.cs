using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IMongoDatabase database, IUksfLogger logger)
{
    private const int Version = 10;

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
        // Old migrations should be deleted once they have run on all environments.
        // They are idempotent but wasteful to re-run on every version bump.
        await MigrateGameServerPlayersToArray();
        await MigratePersistenceSessionDiscriminators();
        await MigrateStatisticsSessionIds();
        await MigrateStatisticsCleanup();
        await MigrateStatisticsComputeFps();
        logger.LogInfo("All migrations completed successfully");
    }

    private async Task MigrateGameServerPlayersToArray()
    {
        logger.LogInfo("Starting migration to convert gameServers status.players from int to array");

        var collection = database.GetCollection<BsonDocument>("gameServers");
        var update = Builders<BsonDocument>.Update.Set("status.players", new BsonArray());

        var result = await collection.UpdateManyAsync(new BsonDocument("status.players", new BsonDocument("$not", new BsonDocument("$type", "array"))), update);

        logger.LogInfo($"Migrated {result.ModifiedCount} game server documents: status.players converted to empty array");
    }

    private async Task MigratePersistenceSessionDiscriminators()
    {
        logger.LogInfo("Starting migration to unwrap BSON discriminators from persistence sessions");
        await PersistenceDataMigration.MigrateAsync(database, msg => logger.LogInfo(msg));
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
}
