using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IMongoDatabase database, IUksfLogger logger)
{
    // When bumping Version and adding a new MigrateXxx step, delete the migrations from the previous version —
    // they have already run on every active db (the version marker prevents them re-running) and operate on
    // collections/fields that may no longer exist. Keep only the migration(s) introduced for the current Version.
    private const int Version = 12;

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
        await MigrateGameConfigExportsToGameDataExports();
        logger.LogInfo("All migrations completed successfully");
    }

    private async Task MigrateGameConfigExportsToGameDataExports()
    {
        var existingNames = await database.ListCollectionNames().ToListAsync();
        if (existingNames.Contains("gameDataExports"))
        {
            logger.LogInfo("MigrateGameConfigExportsToGameDataExports: target collection already exists, skipping");
            return;
        }

        if (!existingNames.Contains("gameConfigExports"))
        {
            logger.LogInfo("MigrateGameConfigExportsToGameDataExports: source collection absent, nothing to migrate");
            return;
        }

        var oldCol = database.GetCollection<BsonDocument>("gameConfigExports");
        var docs = await oldCol.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

        if (docs.Count > 0)
        {
            var newCol = database.GetCollection<BsonDocument>("gameDataExports");
            foreach (var doc in docs)
            {
                var hasConfig = doc.TryGetValue("status", out var statusVal) && IsLegacySuccess(statusVal);
                if (hasConfig)
                {
                    // GameDataExportStatus enum has no BsonRepresentation override, so it serialises as int.
                    // PartialSuccess = 3 in the post-Phase-3 enum order.
                    doc["status"] = (int)GameDataExportStatus.PartialSuccess;
                }

                doc["hasConfig"] = hasConfig;
                doc["hasCbaSettings"] = false;
                doc["hasCbaSettingsReference"] = false;
                doc.Remove("filePath");
            }

            await newCol.InsertManyAsync(docs);
        }

        await database.DropCollectionAsync("gameConfigExports");
        logger.LogInfo($"MigrateGameConfigExportsToGameDataExports: migrated {docs.Count} documents");
    }

    private static bool IsLegacySuccess(BsonValue value)
    {
        if (value is null || value.IsBsonNull)
        {
            return false;
        }

        if (value.IsString)
        {
            return value.AsString == "Success";
        }

        if (value.IsNumeric)
        {
            // Legacy ConfigExportStatus.Success = 2
            return value.AsInt32 == 2;
        }

        return false;
    }
}
