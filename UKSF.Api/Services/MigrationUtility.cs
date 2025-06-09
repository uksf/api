using MongoDB.Bson;
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
        logger.LogInfo("Starting migration to remove old permission fields (readPermissions, writePermissions) and rename roleBasedPermissions to permissions");

        // First, handle folder-level field renaming for all documents
        await documentFolderMetadataContext.UpdateMany(
            x => true, // Update all documents
            Builders<DomainDocumentFolderMetadata>.Update.Combine(
                Builders<DomainDocumentFolderMetadata>.Update.Rename("roleBasedPermissions", "permissions"),
                Builders<DomainDocumentFolderMetadata>.Update.Unset("readPermissions"),
                Builders<DomainDocumentFolderMetadata>.Update.Unset("writePermissions")
            )
        );

        logger.LogInfo("Completed folder-level field operations");

        // For documents array, we need to use aggregation pipeline updates since MongoDB doesn't support 
        // rename operations on array elements. We'll use PipelineUpdateDefinition for this.
        var pipelineStages = new[]
        {
            new BsonDocument(
                "$set",
                new BsonDocument
                {
                    ["documents"] = new BsonDocument(
                        "$map",
                        new BsonDocument
                        {
                            ["input"] = "$documents",
                            ["as"] = "doc",
                            ["in"] = new BsonDocument
                            {
                                ["_id"] = "$$doc._id",
                                ["name"] = "$$doc.name",
                                ["fullPath"] = "$$doc.fullPath",
                                ["created"] = "$$doc.created",
                                ["lastUpdated"] = "$$doc.lastUpdated",
                                ["creator"] = "$$doc.creator",
                                ["owner"] = "$$doc.owner",
                                ["folder"] = "$$doc.folder",
                                ["permissions"] = new BsonDocument(
                                    "$ifNull",
                                    new BsonArray { "$$doc.permissions", "$$doc.roleBasedPermissions" }
                                )
                            }
                        }
                    )
                }
            )
        };

        var pipelineUpdate = Builders<DomainDocumentFolderMetadata>.Update.Pipeline(pipelineStages);

        await documentFolderMetadataContext.UpdateMany(
            x => true, // Update all documents  
            pipelineUpdate
        );

        logger.LogInfo("Completed documents array field migration");

        var folderCount = documentFolderMetadataContext.Get().Count();
        logger.LogInfo($"Successfully processed {folderCount} document folders");
        logger.LogInfo("Old permission fields removal and roleBasedPermissions rename migration completed");
    }
}
