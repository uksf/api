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
    IDocumentPermissionsService documentPermissionsService,
    IDocumentFolderMetadataContext documentFolderMetadataContext,
    IFileContext fileContext,
    IVariablesService variablesService,
    IClock clock,
    IHttpContextService httpContextService,
    IUksfLogger logger
) : IDocumentFolderService
{
    public List<FolderMetadataResponse> GetAllFolders()
    {
        return documentFolderMetadataContext.Get(documentPermissionsService.DoesContextHaveReadPermission).Select(MapFolder).ToList();
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
            if (!documentPermissionsService.DoesContextHaveWritePermission(parentFolderMetadata))
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
            ReadPermissions = createFolder.ReadPermissions,
            WritePermissions = createFolder.WritePermissions
        };

        if (!documentPermissionsService.DoesContextHaveReadPermission(folderMetadata))
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
        if (!documentPermissionsService.DoesContextHaveWritePermission(folderMetadata))
        {
            throw new FolderException($"Cannot edit folder '{folderMetadata.Name}'");
        }

        await documentFolderMetadataContext.Update(folderId, Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Name, newPermissions.Name));
        await documentFolderMetadataContext.Update(
            folderId,
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.WritePermissions, newPermissions.WritePermissions)
        );
        await documentFolderMetadataContext.Update(
            folderId,
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.ReadPermissions, newPermissions.ReadPermissions)
        );

        logger.LogAudit($"Updated folder for {folderMetadata.FullPath}");
        folderMetadata = ValidateAndGetFolder(folderId);
        return MapFolder(folderMetadata);
    }

    public async Task DeleteFolder(string folderId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        if (!documentPermissionsService.DoesContextHaveWritePermission(folderMetadata))
        {
            throw new FolderException($"Cannot delete folder '{folderMetadata.Name}'");
        }

        var folderChildren = GetAllFolderChildren(folderMetadata);
        await Task.WhenAll(folderChildren.Select(DeleteFolder));
        await DeleteFolder(folderMetadata);

        logger.LogAudit($"Deleted folder at {folderMetadata.FullPath}");
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

        if (!documentPermissionsService.DoesContextHaveReadPermission(folderMetadata))
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
        return new FolderMetadataResponse
        {
            Id = folderMetadata.Id,
            Parent = folderMetadata.Parent,
            Name = folderMetadata.Name,
            FullPath = folderMetadata.FullPath,
            Created = folderMetadata.Created,
            Creator = folderMetadata.Creator,
            ReadPermissions = folderMetadata.ReadPermissions,
            WritePermissions = folderMetadata.WritePermissions,
            Documents = folderMetadata.Documents.Select(MapDocument),
            CanWrite = documentPermissionsService.DoesContextHaveWritePermission(folderMetadata)
        };
    }

    private DocumentMetadataResponse MapDocument(DomainDocumentMetadata documentMetadata)
    {
        return new DocumentMetadataResponse
        {
            Id = documentMetadata.Id,
            Folder = documentMetadata.Folder,
            Name = documentMetadata.Name,
            FullPath = documentMetadata.FullPath,
            Created = documentMetadata.Created,
            LastUpdated = documentMetadata.LastUpdated,
            Creator = documentMetadata.Creator,
            ReadPermissions = documentMetadata.ReadPermissions,
            WritePermissions = documentMetadata.WritePermissions,
            CanWrite = documentPermissionsService.DoesContextHaveWritePermission(documentMetadata)
        };
    }
}
