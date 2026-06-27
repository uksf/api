using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IMongoDatabase database, IUksfLogger logger)
{
    // When bumping Version and adding a new MigrateXxx step, delete the migrations from the previous version —
    // they have already run on every active db (the version marker prevents them re-running) and operate on
    // collections/fields that may no longer exist. Keep only the migration(s) introduced for the current Version.
    private const int Version = 13;

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
        await MigrateRemoveOperationCollections();
        logger.LogInfo("All migrations completed successfully");
    }

    private async Task MigrateRemoveOperationCollections()
    {
        await database.DropCollectionAsync("opord");
        await database.DropCollectionAsync("oprep");
        logger.LogInfo("MigrateRemoveOperationCollections: dropped opord and oprep collections");
    }
}
