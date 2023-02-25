using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
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

public class DocumentFolderService : IDocumentFolderService
{
    private readonly IClock _clock;
    private readonly IDocumentFolderMetadataContext _documentFolderMetadataContext;
    private readonly IDocumentPermissionsService _documentPermissionsService;
    private readonly IFileContext _fileContext;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;
    private readonly IVariablesService _variablesService;

    public DocumentFolderService(
        IDocumentPermissionsService documentPermissionsService,
        IDocumentFolderMetadataContext documentFolderMetadataContext,
        IFileContext fileContext,
        IVariablesService variablesService,
        IClock clock,
        IHttpContextService httpContextService,
        IUksfLogger logger
    )
    {
        _documentPermissionsService = documentPermissionsService;
        _documentFolderMetadataContext = documentFolderMetadataContext;
        _fileContext = fileContext;
        _variablesService = variablesService;
        _clock = clock;
        _httpContextService = httpContextService;
        _logger = logger;
    }

    public List<FolderMetadataResponse> GetAllFolders()
    {
        return _documentFolderMetadataContext.Get(x => _documentPermissionsService.DoesContextHaveReadPermission(x)).Select(MapFolder).ToList();
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
            if (!_documentPermissionsService.DoesContextHaveWritePermission(parentFolderMetadata))
            {
                throw new FolderException("Cannot create folder");
            }
        }

        var allFolders = _documentFolderMetadataContext.Get().ToList();
        var folderMetadata = new DomainDocumentFolderMetadata
        {
            Parent = string.IsNullOrEmpty(createFolder.Parent) ? ObjectId.Empty.ToString() : createFolder.Parent,
            Name = createFolder.Name,
            FullPath = ResolveFullFolderPath(allFolders, createFolder.Parent, createFolder.Name),
            Created = _clock.UtcNow(),
            Creator = _httpContextService.GetUserId(),
            ReadPermissions = createFolder.ReadPermissions,
            WritePermissions = createFolder.WritePermissions
        };

        if (!_documentPermissionsService.DoesContextHaveReadPermission(folderMetadata))
        {
            throw new FolderException("Cannot create folder you won't be able to view");
        }

        if (allFolders.Any(x => x.FullPath.EqualsIgnoreCase(folderMetadata.FullPath)))
        {
            throw new FolderException($"A folder already exists at path '{folderMetadata.FullPath}'");
        }

        await _documentFolderMetadataContext.Add(folderMetadata);

        _logger.LogAudit($"Created folder at {folderMetadata.FullPath}");
        return MapFolder(folderMetadata);
    }

    public async Task<FolderMetadataResponse> UpdateFolder(string folderId, CreateFolderRequest newPermissions)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        if (!_documentPermissionsService.DoesContextHaveWritePermission(folderMetadata))
        {
            throw new FolderException("Cannot edit folder");
        }

        await _documentFolderMetadataContext.Update(folderId, Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Name, newPermissions.Name));
        await _documentFolderMetadataContext.Update(
            folderId,
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.WritePermissions, newPermissions.WritePermissions)
        );
        await _documentFolderMetadataContext.Update(
            folderId,
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.ReadPermissions, newPermissions.ReadPermissions)
        );

        _logger.LogAudit($"Updated folder for {folderMetadata.FullPath}");
        folderMetadata = ValidateAndGetFolder(folderId);
        return MapFolder(folderMetadata);
    }

    public async Task DeleteFolder(string folderId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        if (!_documentPermissionsService.DoesContextHaveWritePermission(folderMetadata))
        {
            throw new FolderException("Cannot delete folder");
        }

        var folderChildren = GetAllFolderChildren(folderMetadata);
        await Task.WhenAll(folderChildren.Select(DeleteFolder));
        await DeleteFolder(folderMetadata);

        _logger.LogAudit($"Deleted folder at {folderMetadata.FullPath}");
    }

    private static string ResolveFullFolderPath(IEnumerable<DomainDocumentFolderMetadata> allFolders, string parent, string name)
    {
        var parentFolder = allFolders.FirstOrDefault(x => x.Id == parent);
        return parentFolder == null ? name : Path.Combine(parentFolder.FullPath, name);
    }

    private DomainDocumentFolderMetadata ValidateAndGetFolder(string folderId)
    {
        var folderMetadata = _documentFolderMetadataContext.GetSingle(folderId);
        if (folderMetadata == null)
        {
            throw new FolderNotFoundException($"Folder with ID '{folderId}' not found");
        }

        if (!_documentPermissionsService.DoesContextHaveReadPermission(folderMetadata))
        {
            throw new FolderException("Cannot view folder");
        }

        return folderMetadata;
    }

    private List<DomainDocumentFolderMetadata> GetAllFolderChildren(DomainDocumentFolderMetadata folderMetadata)
    {
        var children = _documentFolderMetadataContext.Get(x => x.Parent == folderMetadata.Id).ToList();
        children.AddRange(children.SelectMany(GetAllFolderChildren).ToList());
        return children;
    }

    private Task DeleteFolder(DomainDocumentFolderMetadata folderMetadata)
    {
        folderMetadata.Documents.ForEach(x => RenameDocumentFile(x.Id));
        return _documentFolderMetadataContext.Delete(folderMetadata.Id);
    }

    private void RenameDocumentFile(string documentId)
    {
        var documentsPath = _variablesService.GetVariable("DOCUMENTS_PATH").AsString();
        var documentPath = Path.Combine(documentsPath, $"{documentId}.json");
        var newPath = Path.Combine(documentsPath, "__DELETED", $"{documentId}.json");
        _fileContext.Rename(documentPath, newPath);
    }

    private FolderMetadataResponse MapFolder(DomainDocumentFolderMetadata folderMetadata)
    {
        return new()
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
            CanWrite = _documentPermissionsService.DoesContextHaveWritePermission(folderMetadata)
        };
    }

    private DocumentMetadataResponse MapDocument(DomainDocumentMetadata documentMetadata)
    {
        return new()
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
            CanWrite = _documentPermissionsService.DoesContextHaveWritePermission(documentMetadata)
        };
    }
}
