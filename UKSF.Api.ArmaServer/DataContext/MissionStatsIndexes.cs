using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.DataContext;

public class MissionStatsIndexes(IMongoDatabase database, IUksfLogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateIndex<MissionSession>(
                "missionSessions",
                Builders<MissionSession>.IndexKeys.Ascending(x => x.Mission).Ascending(x => x.Map).Descending(x => x.LastBatchReceived),
                "ix_mission_map_lastBatch"
            );

            await CreateIndex<PlayerMissionStats>(
                "playerMissionStats",
                Builders<PlayerMissionStats>.IndexKeys.Ascending(x => x.MissionSessionId).Ascending(x => x.PlayerUid),
                "ix_sessionId_playerUid",
                unique: true
            );

            await CreateIndex<MissionStats>("missionStats", Builders<MissionStats>.IndexKeys.Ascending(x => x.MissionSessionId), "ix_sessionId", unique: true);

            await CreateIndex<MissionStatsBatch>(
                "missionStatsBatches",
                Builders<MissionStatsBatch>.IndexKeys.Ascending(x => x.MissionSessionId),
                "ix_sessionId"
            );

            logger.LogInfo("Mission stats indexes ensured");
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to create mission stats indexes", ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateIndex<T>(string collectionName, IndexKeysDefinition<T> keys, string indexName, bool unique = false)
    {
        var collection = database.GetCollection<T>(collectionName);
        var model = new CreateIndexModel<T>(
            keys,
            new CreateIndexOptions
            {
                Name = indexName,
                Unique = unique,
                Background = true
            }
        );
        await collection.Indexes.CreateOneAsync(model);
    }
}
