using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IDocumentFolderMetadataContext documentFolderMetadataContext, IUksfLogger logger)
{
    private const int Version = 6;

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
        await FixDocumentOwnerObjectIdTypes();
        logger.LogInfo("All migrations completed successfully");
    }

    private async Task FixDocumentOwnerObjectIdTypes()
    {
        logger.LogInfo("Starting migration to fix document owner ObjectId storage types");

        // Get ALL documents to fix owner field storage types
        var allDocuments = documentFolderMetadataContext.Get().ToList();

        logger.LogInfo($"Found {allDocuments.Count} document folders to check for owner field fixes");

        foreach (var folder in allDocuments)
        {
            var updates = new List<UpdateDefinition<DomainDocumentFolderMetadata>>();

            // Fix documents within folders where owner might be stored as string instead of ObjectId
            for (var i = 0; i < folder.Documents.Count; i++)
            {
                var document = folder.Documents[i];

                // Fix owner field - ensure it's stored as ObjectId type in MongoDB
                if (!string.IsNullOrEmpty(document.Owner))
                {
                    // Convert string to ObjectId to ensure proper BSON storage
                    if (ObjectId.TryParse(document.Owner, out var ownerObjectId))
                    {
                        updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Set($"documents.{i}.owner", ownerObjectId));
                    }
                }

                // Also fix creator field if needed
                if (!string.IsNullOrEmpty(document.Creator))
                {
                    if (ObjectId.TryParse(document.Creator, out var creatorObjectId))
                    {
                        updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Set($"documents.{i}.creator", creatorObjectId));
                    }
                }
            }

            // Fix folder-level owner and creator fields as well
            if (!string.IsNullOrEmpty(folder.Owner))
            {
                if (ObjectId.TryParse(folder.Owner, out var folderOwnerObjectId))
                {
                    updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Set("owner", folderOwnerObjectId));
                }
            }

            if (!string.IsNullOrEmpty(folder.Creator))
            {
                if (ObjectId.TryParse(folder.Creator, out var folderCreatorObjectId))
                {
                    updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Set("creator", folderCreatorObjectId));
                }
            }

            if (updates.Count != 0)
            {
                var combinedUpdate = Builders<DomainDocumentFolderMetadata>.Update.Combine(updates);
                await documentFolderMetadataContext.Update(folder.Id, combinedUpdate);
                logger.LogInfo($"Fixed ObjectId types for document folder: {folder.Name} (ID: {folder.Id}) - Fixed {folder.Documents.Count} documents");
            }
        }

        logger.LogInfo($"Successfully fixed ObjectId storage types for {allDocuments.Count} document folders");
        logger.LogInfo("Document owner ObjectId type fix migration completed");
    }
}
