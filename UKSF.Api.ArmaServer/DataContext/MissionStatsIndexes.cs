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
            await CreateIndex<MissionSession>("missionSessions", Builders<MissionSession>.IndexKeys.Ascending(x => x.SessionId), "ix_sessionId", unique: true);

            await CreateIndex<PlayerMissionStats>(
                "playerMissionStats",
                Builders<PlayerMissionStats>.IndexKeys.Ascending(x => x.MissionSessionId).Ascending(x => x.PlayerUid),
                "ix_sessionId_playerUid",
                unique: true
            );

            await CreateIndex<MissionStats>("missionStats", Builders<MissionStats>.IndexKeys.Ascending(x => x.MissionSessionId), "ix_sessionId", unique: true);

            await CreateIndex<MissionStatsEventsSampler>(
                "missionStatsEventsSampler",
                Builders<MissionStatsEventsSampler>.IndexKeys.Ascending(x => x.MissionSessionId).Ascending(x => x.PlayerUid),
                "ix_sessionId_playerUid",
                unique: true
            );

            await CreateIndex<MissionStatsEventsCombat>(
                "missionStatsEventsCombat",
                Builders<MissionStatsEventsCombat>.IndexKeys.Ascending(x => x.MissionSessionId).Ascending(x => x.BucketIndex),
                "ix_sessionId_bucketIndex",
                unique: true
            );

            await CreateIndex<MissionStatsEventsLifecycle>(
                "missionStatsEventsLifecycle",
                Builders<MissionStatsEventsLifecycle>.IndexKeys.Ascending(x => x.MissionSessionId),
                "ix_sessionId",
                unique: true
            );
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
        var model = new CreateIndexModel<T>(keys, new CreateIndexOptions { Name = indexName, Unique = unique });
        await collection.Indexes.CreateOneAsync(model);
    }
}
