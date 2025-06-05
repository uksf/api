using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IDocumentFolderMetadataContext documentFolderMetadataContext, IUksfLogger logger)
{
    private const int Version = 5;

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
        await MigrateDocumentPermissions();
        logger.LogInfo("All migrations completed successfully");
    }

    private async Task MigrateDocumentPermissions()
    {
        logger.LogInfo("Starting document permissions migration to role-based system");

        // Get ALL documents to ensure RoleBasedPermissions is added to every entry
        var allDocuments = documentFolderMetadataContext.Get().ToList();

        logger.LogInfo($"Found {allDocuments.Count} document folders to migrate");

        foreach (var folder in allDocuments)
        {
            var updates = new List<UpdateDefinition<DomainDocumentFolderMetadata>>();

            // Set Owner to Creator if not already set
            if (string.IsNullOrEmpty(folder.Owner) && !string.IsNullOrEmpty(folder.Creator))
            {
                updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Owner, folder.Creator));
            }

            // Convert legacy permissions to role-based permissions for folders
            var folderRoleBasedPermissions = new RoleBasedDocumentPermissions
            {
                Viewers = new PermissionRole { Units = folder.ReadPermissions.Units ?? [], Rank = folder.ReadPermissions.Rank ?? string.Empty }
            };

            // Migrate WritePermissions to Collaborators
            if (folder.WritePermissions != null)
            {
                folderRoleBasedPermissions.Collaborators = new PermissionRole
                {
                    Units = folder.WritePermissions.Units ?? [], Rank = folder.WritePermissions.Rank ?? string.Empty
                };
            }

            updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.RoleBasedPermissions, folderRoleBasedPermissions));

            // Migrate documents within folders
            for (var i = 0; i < folder.Documents.Count; i++)
            {
                var document = folder.Documents[i];

                // Set Owner to Creator if not already set for documents
                if (string.IsNullOrEmpty(document.Owner) && !string.IsNullOrEmpty(document.Creator))
                {
                    updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Set($"documents.{i}.owner", document.Creator));
                }

                // Convert legacy permissions to role-based permissions for documents
                var documentRoleBasedPermissions = new RoleBasedDocumentPermissions();

                // Migrate ReadPermissions to Viewers
                if (document.ReadPermissions != null)
                {
                    documentRoleBasedPermissions.Viewers = new PermissionRole
                    {
                        Units = document.ReadPermissions.Units ?? [], Rank = document.ReadPermissions.Rank ?? string.Empty
                    };
                }

                // Migrate WritePermissions to Collaborators
                if (document.WritePermissions != null)
                {
                    documentRoleBasedPermissions.Collaborators = new PermissionRole
                    {
                        Units = document.WritePermissions.Units ?? [], Rank = document.WritePermissions.Rank ?? string.Empty
                    };
                }

                updates.Add(Builders<DomainDocumentFolderMetadata>.Update.Set($"documents.{i}.roleBasedPermissions", documentRoleBasedPermissions));
            }

            if (updates.Count != 0)
            {
                var combinedUpdate = Builders<DomainDocumentFolderMetadata>.Update.Combine(updates);
                await documentFolderMetadataContext.Update(folder.Id, combinedUpdate);
                logger.LogInfo($"Updated document folder: {folder.Name} (ID: {folder.Id}) - Migrated {folder.Documents.Count} documents");
            }
        }

        logger.LogInfo($"Successfully migrated {allDocuments.Count} document folders to role-based permission system");
        logger.LogInfo("Document permissions migration completed - legacy permissions fields retained for backwards compatibility");
    }
}
