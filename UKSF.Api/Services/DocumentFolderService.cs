using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;

namespace UKSF.Api.Services;

public interface IDocumentFolderService
{
    List<FolderMetadataResponse> GetAllFolders();
    Task<FolderMetadataResponse> GetFolder(string folderId);
    Task<FolderMetadataResponse> CreateFolder(CreateFolderRequest createFolder);
    Task<FolderMetadataResponse> UpdateFolder(string folderId, CreateFolderRequest newPermissions);
    Task DeleteFolder(string folderId);
}

public class DocumentFolderService(
    IDocumentFolderMetadataContext documentFolderMetadataContext,
    IHttpContextService httpContextService,
    IDocumentPermissionsService documentPermissionsService,
    IFileContext fileContext,
    IVariablesService variablesService,
    IClock clock,
    IUksfLogger logger
) : IDocumentFolderService
{
    public List<FolderMetadataResponse> GetAllFolders()
    {
        // Get all folders that the user can read - this is already using the cached permissions service
        var accessibleFolders = documentFolderMetadataContext.Get(documentPermissionsService.CanContextView).ToList();

        // Batch process all folders with optimized mapping
        return accessibleFolders.Select(MapFolder).ToList();
    }

    public Task<FolderMetadataResponse> GetFolder(string folderId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        return Task.FromResult(MapFolder(folderMetadata));
    }

    public async Task<FolderMetadataResponse> CreateFolder(CreateFolderRequest createFolder)
    {
        if (createFolder.Parent != ObjectId.Empty.ToString())
        {
            var parentFolderMetadata = ValidateAndGetFolder(createFolder.Parent);
            if (!documentPermissionsService.CanContextCollaborate(parentFolderMetadata))
            {
                throw new FolderException("Cannot create folder");
            }
        }

        var allFolders = documentFolderMetadataContext.Get().ToList();
        var folderMetadata = new DomainDocumentFolderMetadata
        {
            Parent = string.IsNullOrEmpty(createFolder.Parent) ? ObjectId.Empty.ToString() : createFolder.Parent,
            Name = createFolder.Name,
            FullPath = ResolveFullFolderPath(allFolders, createFolder.Parent, createFolder.Name),
            Created = clock.UtcNow(),
            Creator = httpContextService.GetUserId(),
            Owner = createFolder.Owner ?? httpContextService.GetUserId(),
            Permissions = createFolder.Permissions
        };

        if (!documentPermissionsService.CanContextView(folderMetadata))
        {
            throw new FolderException("Cannot create folder you won't be able to view");
        }

        if (allFolders.Any(x => x.FullPath.EqualsIgnoreCase(folderMetadata.FullPath)))
        {
            throw new FolderException($"A folder already exists at path '{folderMetadata.FullPath}'");
        }

        await documentFolderMetadataContext.Add(folderMetadata);

        logger.LogAudit($"Created folder at {folderMetadata.FullPath}");
        return MapFolder(folderMetadata);
    }

    public async Task<FolderMetadataResponse> UpdateFolder(string folderId, CreateFolderRequest newPermissions)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        if (!documentPermissionsService.CanContextCollaborate(folderMetadata))
        {
            throw new FolderException($"Cannot edit folder '{folderMetadata.Name}'");
        }

        // Calculate new full path
        var allFolders = documentFolderMetadataContext.Get().ToList();
        var newFullPath = ResolveFullFolderPath(allFolders, folderMetadata.Parent, newPermissions.Name);

        // Check for name collision
        if (allFolders.Any(x => x.Id != folderId && x.FullPath.EqualsIgnoreCase(newFullPath)))
        {
            throw new FolderException($"A folder already exists at path '{newFullPath}'");
        }

        // Update permission fields, including FullPath
        var updates = new List<UpdateDefinition<DomainDocumentFolderMetadata>>
        {
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Name, newPermissions.Name),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.FullPath, newFullPath),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Owner, newPermissions.Owner ?? folderMetadata.Owner),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Permissions, newPermissions.Permissions)
        };

        var combinedUpdate = Builders<DomainDocumentFolderMetadata>.Update.Combine(updates);
        await documentFolderMetadataContext.Update(folderId, combinedUpdate);

        // If the folder name/path changed, update all child folders and documents
        if (!folderMetadata.FullPath.EqualsIgnoreCase(newFullPath))
        {
            await UpdateChildFolderPaths(folderId, folderMetadata.FullPath, newFullPath);
        }

        logger.LogAudit($"Updated folder for {newFullPath}");
        folderMetadata = ValidateAndGetFolder(folderId);
        return MapFolder(folderMetadata);
    }

    public async Task DeleteFolder(string folderId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        if (!documentPermissionsService.CanContextCollaborate(folderMetadata))
        {
            throw new FolderException($"Cannot delete folder '{folderMetadata.Name}'");
        }

        var folderChildren = GetAllFolderChildren(folderMetadata);
        await Task.WhenAll(folderChildren.Select(DeleteFolder));
        await DeleteFolder(folderMetadata);

        logger.LogAudit($"Deleted folder at {folderMetadata.FullPath}");
    }

    private async Task UpdateChildFolderPaths(string parentFolderId, string oldParentPath, string newParentPath)
    {
        // Get all child folders of this parent
        var childFolders = documentFolderMetadataContext.Get(x => x.Parent == parentFolderId).ToList();

        if (childFolders.Count == 0)
        {
            return; // No child folders to update
        }

        var updateTasks = new List<Task>();

        foreach (var childFolder in childFolders)
        {
            // Calculate new path for this child folder
            var newChildPath = childFolder.FullPath.Replace(oldParentPath, newParentPath);

            // Add folder update task
            updateTasks.Add(
                documentFolderMetadataContext.Update(childFolder.Id, Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.FullPath, newChildPath))
            );

            // Add document update tasks for this folder
            foreach (var document in childFolder.Documents)
            {
                var newDocumentPath = document.FullPath.Replace(oldParentPath, newParentPath);
                updateTasks.Add(
                    documentFolderMetadataContext.FindAndUpdate(
                        x => x.Id == childFolder.Id && x.Documents.Any(d => d.Id == document.Id),
                        Builders<DomainDocumentFolderMetadata>.Update.Set("Documents.$.FullPath", newDocumentPath)
                    )
                );
            }
        }

        // Execute all updates in parallel
        await Task.WhenAll(updateTasks);

        // Recursively update child folders (done after current level is complete)
        var recursiveTasks = childFolders.Select(childFolder => UpdateChildFolderPaths(childFolder.Id, oldParentPath, newParentPath));
        await Task.WhenAll(recursiveTasks);
    }

    private static string ResolveFullFolderPath(IEnumerable<DomainDocumentFolderMetadata> allFolders, string parent, string name)
    {
        var parentFolder = allFolders.FirstOrDefault(x => x.Id == parent);
        return parentFolder == null ? name : Path.Combine(parentFolder.FullPath, name);
    }

    private DomainDocumentFolderMetadata ValidateAndGetFolder(string folderId)
    {
        var folderMetadata = documentFolderMetadataContext.GetSingle(folderId);
        if (folderMetadata == null)
        {
            throw new FolderNotFoundException($"Folder with ID '{folderId}' not found");
        }

        if (!documentPermissionsService.CanContextView(folderMetadata))
        {
            throw new FolderException($"Cannot view folder '{folderMetadata.Name}'");
        }

        return folderMetadata;
    }

    private List<DomainDocumentFolderMetadata> GetAllFolderChildren(DomainDocumentFolderMetadata folderMetadata)
    {
        var children = documentFolderMetadataContext.Get(x => x.Parent == folderMetadata.Id).ToList();
        children.AddRange(children.SelectMany(GetAllFolderChildren).ToList());
        return children;
    }

    private Task DeleteFolder(DomainDocumentFolderMetadata folderMetadata)
    {
        folderMetadata.Documents.ForEach(x => RenameDocumentFile(x.Id));
        return documentFolderMetadataContext.Delete(folderMetadata.Id);
    }

    private void RenameDocumentFile(string documentId)
    {
        var documentsPath = variablesService.GetVariable("DOCUMENTS_PATH").AsString();
        var documentPath = Path.Combine(documentsPath, $"{documentId}.json");
        var newPath = Path.Combine(documentsPath, "__DELETED", $"{documentId}.json");
        fileContext.Rename(documentPath, newPath);
    }

    private FolderMetadataResponse MapFolder(DomainDocumentFolderMetadata folderMetadata)
    {
        var effectivePermissions = documentPermissionsService.GetEffectivePermissions(folderMetadata);
        var inheritedPermissions = documentPermissionsService.GetInheritedPermissions(folderMetadata);
        var canWrite = documentPermissionsService.CanContextCollaborate(folderMetadata);

        var documentsInheritedPermissions = folderMetadata.Documents.Count > 0
            ? documentPermissionsService.GetInheritedPermissionsFromHierarchy(folderMetadata.Id)
            : null;

        var accessibleDocuments = folderMetadata.Documents.Where(documentPermissionsService.CanContextView)
                                                .Select(doc => MapDocument(doc, documentsInheritedPermissions))
                                                .ToList();

        return new FolderMetadataResponse
        {
            Id = folderMetadata.Id,
            Parent = folderMetadata.Parent,
            Name = folderMetadata.Name,
            FullPath = folderMetadata.FullPath,
            Created = folderMetadata.Created,
            Creator = folderMetadata.Creator,
            Owner = folderMetadata.Owner,
            Permissions = folderMetadata.Permissions,
            EffectivePermissions = effectivePermissions,
            InheritedPermissions = inheritedPermissions,
            Documents = accessibleDocuments,
            CanWrite = canWrite
        };
    }

    private DocumentMetadataResponse MapDocument(DomainDocumentMetadata documentMetadata, DocumentPermissions sharedInheritedPermissions)
    {
        var effectivePermissions = documentPermissionsService.GetEffectivePermissions(documentMetadata);
        var canWrite = documentPermissionsService.CanContextCollaborate(documentMetadata);

        return new DocumentMetadataResponse
        {
            Id = documentMetadata.Id,
            Folder = documentMetadata.Folder,
            Name = documentMetadata.Name,
            FullPath = documentMetadata.FullPath,
            Created = documentMetadata.Created,
            LastUpdated = documentMetadata.LastUpdated,
            Creator = documentMetadata.Creator,
            Owner = documentMetadata.Owner,
            Permissions = documentMetadata.Permissions,
            EffectivePermissions = effectivePermissions,
            InheritedPermissions = sharedInheritedPermissions,
            CanWrite = canWrite
        };
    }
}
