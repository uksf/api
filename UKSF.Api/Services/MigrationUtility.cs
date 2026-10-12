using MongoDB.Driver;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IMongoDatabase database, IUksfLogger logger)
{
    private const int Version = 14;

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
        await MigrateStructuredPersistenceMarkers();
        logger.LogInfo("All migrations completed successfully");
    }

    private async Task MigrateStructuredPersistenceMarkers()
    {
        var migrated = await PersistenceMarkerMigration.MigrateAsync(database);
        logger.LogInfo($"MigrateStructuredPersistenceMarkers: converted markers in {migrated} persistence sessions");
    }
}
