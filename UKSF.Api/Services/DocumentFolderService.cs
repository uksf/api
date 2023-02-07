using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;

namespace UKSF.Api.Services;

public interface IDocumentFolderService
{
    List<DomainDocumentFolderMetadata> GetAllFolders();
    Task<DomainDocumentFolderMetadata> GetFolder(string folderId);
    Task<DomainDocumentFolderMetadata> CreateFolder(CreateFolderRequest createFolder);
    Task<DomainDocumentFolderMetadata> UpdateFolderPermissions(string folderId, UpdateDocumentPermissionsRequest newPermissions);
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

    public List<DomainDocumentFolderMetadata> GetAllFolders()
    {
        return _documentFolderMetadataContext.Get(x => _documentPermissionsService.DoesContextHaveReadPermission(x.ReadPermissions)).ToList();
    }

    public Task<DomainDocumentFolderMetadata> GetFolder(string folderId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);

        return Task.FromResult(folderMetadata);
    }

    public async Task<DomainDocumentFolderMetadata> CreateFolder(CreateFolderRequest createFolder)
    {
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

        ValidateFolderWritePermissions(folderMetadata);

        if (allFolders.Any(x => x.FullPath.EqualsIgnoreCase(folderMetadata.FullPath)))
        {
            throw new FolderException($"A folder already exists at path '{folderMetadata.FullPath}'");
        }

        await _documentFolderMetadataContext.Add(folderMetadata);

        _logger.LogAudit($"Created folder at {folderMetadata.FullPath}");
        return folderMetadata;
    }

    public async Task<DomainDocumentFolderMetadata> UpdateFolderPermissions(string folderId, UpdateDocumentPermissionsRequest newPermissions)
    {
        var folderMetadata = ValidateAndGetFolder(folderId, true);
        await _documentFolderMetadataContext.Update(
            folderId,
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.WritePermissions, newPermissions.WritePermissions)
        );
        await _documentFolderMetadataContext.Update(
            folderId,
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.ReadPermissions, newPermissions.ReadPermissions)
        );

        _logger.LogAudit($"Updated folder permissions for {folderMetadata.FullPath}");
        return ValidateAndGetFolder(folderId);
    }

    public async Task DeleteFolder(string folderId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId, true);
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

    private DomainDocumentFolderMetadata ValidateAndGetFolder(string folderId, bool requireWritePermission = false)
    {
        var folderMetadata = _documentFolderMetadataContext.GetSingle(folderId);
        if (folderMetadata == null)
        {
            throw new FolderNotFoundException($"Folder with ID '{folderId}' not found");
        }

        if (requireWritePermission)
        {
            ValidateFolderWritePermissions(folderMetadata);
            return folderMetadata;
        }

        ValidateFolderReadPermissions(folderMetadata);
        return folderMetadata;
    }

    private List<DomainDocumentFolderMetadata> GetAllFolderChildren(DomainDocumentFolderMetadata folderMetadata)
    {
        var children = _documentFolderMetadataContext.Get(x => x.Parent == folderMetadata.Id).ToList();
        children.AddRange(children.SelectMany(GetAllFolderChildren));
        return children;
    }

    private void ValidateFolderReadPermissions(DomainDocumentFolderMetadata folderMetadata)
    {
        if (!_documentPermissionsService.DoesContextHaveReadPermission(folderMetadata.ReadPermissions))
        {
            throw new FolderAccessDeniedException();
        }
    }

    private void ValidateFolderWritePermissions(DomainDocumentFolderMetadata folderMetadata)
    {
        if (!_documentPermissionsService.DoesContextHaveWritePermission(folderMetadata.WritePermissions))
        {
            throw new FolderAccessDeniedException();
        }
    }

    private Task DeleteFolder(DomainDocumentFolderMetadata folderMetadata)
    {
        folderMetadata.Documents.ForEach(x => RenameDocumentFile(x.Id));
        return _documentFolderMetadataContext.Delete(folderMetadata.Id);
    }

    private void RenameDocumentFile(string documentId)
    {
        var documentsPath = _variablesService.GetVariable("DOCUMENTS_PATH").AsString();
        var documentPath = Path.Combine(documentsPath, $"{documentId}.md");
        var newPath = Path.Combine(documentsPath, "__DELETED", $"{documentId}.md");
        _fileContext.Rename(documentPath, newPath);
    }
}
