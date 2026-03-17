using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IMongoDatabase database, IUksfLogger logger)
{
    private const int Version = 9;

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
        await MigrateGameServerPlayersToArray();
        await MigratePersistenceSessionDiscriminators();
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
}
