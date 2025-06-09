using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IDocumentFolderMetadataContext documentFolderMetadataContext, IUksfLogger logger)
{
    private const int Version = 7;

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
        await RemoveOldPermissionFields();
        logger.LogInfo("All migrations completed successfully");
    }

    private async Task RemoveOldPermissionFields()
    {
        logger.LogInfo("Starting migration to remove old permission fields (readPermissions, writePermissions)");

        // Get ALL document folders to remove old permission fields
        var allFolders = documentFolderMetadataContext.Get().ToList();

        logger.LogInfo($"Found {allFolders.Count} document folders to clean up");

        foreach (var folder in allFolders)
        {
            var updates = new List<UpdateDefinition<DomainDocumentFolderMetadata>>();

            // Remove old permission fields from the folder itself
            updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Unset("readPermissions"));
            updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Unset("writePermissions"));

            // Remove old permission fields from documents within the folder
            for (var i = 0; i < folder.Documents.Count; i++)
            {
                updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Unset($"documents.{i}.readPermissions"));
                updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Unset($"documents.{i}.writePermissions"));
            }

            if (updates.Count > 0)
            {
                var combinedUpdate = Builders<DomainDocumentFolderMetadata>.Update.Combine(updates);
                await documentFolderMetadataContext.Update(folder.Id, combinedUpdate);
                logger.LogInfo($"Cleaned up old permission fields for folder: {folder.Name} (ID: {folder.Id}) - Cleaned {folder.Documents.Count} documents");
            }
        }

        logger.LogInfo($"Successfully removed old permission fields from {allFolders.Count} document folders");
        logger.LogInfo("Old permission fields removal migration completed");
    }
}
