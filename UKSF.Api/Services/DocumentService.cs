using MongoDB.Driver;
using MongoDB.Driver.Linq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;

namespace UKSF.Api.Services;

public interface IDocumentService
{
    Task<DomainDocumentMetadata> GetDocument(string folderId, string documentId);
    Task<DomainDocumentMetadata> CreateDocument(string folderId, CreateDocumentRequest createDocument);
    Task<DomainDocumentMetadata> UpdateDocumentPermissions(string folderId, string documentId, UpdateDocumentPermissionsRequest newPermissions);
    Task DeleteDocument(string folderId, string documentId);

    Task<DocumentContentResponse> GetDocumentContent(string folderId, string documentId);
    Task<DocumentContentResponse> UpdateDocumentContent(string folderId, string documentId, UpdateDocumentContentRequest updateDocumentContent);
}

public class DocumentService : IDocumentService
{
    private readonly IClock _clock;
    private readonly IDocumentFolderMetadataContext _documentFolderMetadataContext;
    private readonly IDocumentPermissionsService _documentPermissionsService;
    private readonly IFileContext _fileContext;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;
    private readonly IVariablesService _variablesService;

    public DocumentService(
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

    public Task<DomainDocumentMetadata> GetDocument(string folderId, string documentId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId);

        return Task.FromResult(documentMetadata);
    }

    public async Task<DomainDocumentMetadata> CreateDocument(string folderId, CreateDocumentRequest createDocument)
    {
        var folderMetadata = ValidateAndGetFolder(folderId, true);
        var documentMetadata = new DomainDocumentMetadata
        {
            Folder = folderId,
            Name = createDocument.Name,
            FullPath = Path.Combine(folderMetadata.FullPath, createDocument.Name),
            Created = _clock.UtcNow(),
            LastUpdated = _clock.UtcNow(),
            Creator = _httpContextService.GetUserId(),
            ReadPermissions = createDocument.ReadPermissions,
            WritePermissions = createDocument.WritePermissions
        };
        ValidateDocumentWritePermissions(documentMetadata);

        if (folderMetadata.Documents.Any(x => x.Name.EqualsIgnoreCase(documentMetadata.Name)))
        {
            throw new DocumentException($"A document already exists at path '{documentMetadata.FullPath}'");
        }

        await _documentFolderMetadataContext.Update(folderId, Builders<DomainDocumentFolderMetadata>.Update.Push(x => x.Documents, documentMetadata));
        await CreateDocumentFile(documentMetadata.Id);

        _logger.LogAudit($"Created document at {documentMetadata.FullPath}");
        return documentMetadata;
    }

    public async Task<DomainDocumentMetadata> UpdateDocumentPermissions(string folderId, string documentId, UpdateDocumentPermissionsRequest newPermissions)
    {
        var folderMetadata = ValidateAndGetFolder(folderId, true);
        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId, true);

        await _documentFolderMetadataContext.FindAndUpdate(
            x => x.Id == folderId && x.Documents.Any(y => y.Id == documentId),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Documents.FirstMatchingElement().WritePermissions, newPermissions.WritePermissions)
        );
        await _documentFolderMetadataContext.FindAndUpdate(
            x => x.Id == folderId && x.Documents.Any(y => y.Id == documentId),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Documents.FirstMatchingElement().ReadPermissions, newPermissions.ReadPermissions)
        );

        _logger.LogAudit($"Updated document permissions for {documentMetadata.FullPath}");
        return ValidateAndGetDocument(folderMetadata, documentId);
    }

    public async Task DeleteDocument(string folderId, string documentId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId, true);
        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId, true);
        ValidateAndGetDocumentPath(documentMetadata.Id);

        await _documentFolderMetadataContext.Update(
            folderId,
            Builders<DomainDocumentFolderMetadata>.Update.PullFilter(x => x.Documents, x => x.Id == documentMetadata.Id)
        );
        RenameDocumentFile(documentId);

        _logger.LogAudit($"Deleted document at {documentMetadata.FullPath}");
    }

    public async Task<DocumentContentResponse> GetDocumentContent(string folderId, string documentId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId);
        var documentPath = ValidateAndGetDocumentPath(documentMetadata.Id);
        var text = await _fileContext.ReadAllText(documentPath);
        return new() { Text = text, LastUpdated = documentMetadata.LastUpdated };
    }

    public async Task<DocumentContentResponse> UpdateDocumentContent(string folderId, string documentId, UpdateDocumentContentRequest updateDocumentContent)
    {
        var folderMetadata = ValidateAndGetFolder(folderId, true);
        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId, true);
        if (updateDocumentContent.LastKnownUpdated < documentMetadata.LastUpdated)
        {
            throw new DocumentException($"Document update for '{documentMetadata.Name}' is behind more recent changes. Please refresh");
        }

        var updated = _clock.UtcNow();
        await _documentFolderMetadataContext.FindAndUpdate(
            x => x.Id == folderId && x.Documents.Any(y => y.Id == documentId),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Documents.FirstMatchingElement().LastUpdated, updated)
        );

        var documentPath = ValidateAndGetDocumentPath(documentMetadata.Id);
        await _fileContext.WriteTextToFile(documentPath, updateDocumentContent.NewText);

        _logger.LogAudit($"Updated document at {documentMetadata.FullPath}");
        return new() { Text = updateDocumentContent.NewText, LastUpdated = updated };
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

    private DomainDocumentMetadata ValidateAndGetDocument(DomainDocumentFolderMetadata folderMetadata, string documentId, bool requireWritePermission = false)
    {
        var documentMetadata = folderMetadata.Documents.FirstOrDefault(x => x.Id == documentId);
        if (documentMetadata == null)
        {
            throw new DocumentNotFoundException($"Document with ID '{documentId}' not found");
        }

        if (requireWritePermission)
        {
            ValidateDocumentWritePermissions(documentMetadata);
            return documentMetadata;
        }

        ValidateDocumentReadPermissions(documentMetadata);
        return documentMetadata;
    }

    private void ValidateDocumentReadPermissions(DomainDocumentMetadata documentMetadata)
    {
        if (!_documentPermissionsService.DoesContextHaveReadPermission(documentMetadata.ReadPermissions))
        {
            throw new DocumentAccessDeniedException();
        }
    }

    private void ValidateDocumentWritePermissions(DomainDocumentMetadata documentMetadata)
    {
        if (!_documentPermissionsService.DoesContextHaveWritePermission(documentMetadata.WritePermissions))
        {
            throw new DocumentAccessDeniedException();
        }
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

    private string ValidateAndGetDocumentPath(string documentId)
    {
        var documentPath = FormatDocumentPath(documentId);
        if (!_fileContext.Exists(documentPath))
        {
            throw new DocumentNotFoundException($"No document file found for '{documentPath}'");
        }

        return documentPath;
    }

    private Task CreateDocumentFile(string documentId)
    {
        var documentPath = FormatDocumentPath(documentId);
        _fileContext.CreateFile(documentPath);
        return _fileContext.WriteTextToFile(documentPath, "Markdown supported!\nStart adding some text...");
    }

    private void RenameDocumentFile(string documentId)
    {
        var documentsPath = _variablesService.GetVariable("DOCUMENTS_PATH").AsString();
        var documentPath = Path.Combine(documentsPath, $"{documentId}.md");
        var newPath = Path.Combine(documentsPath, "__DELETED", $"{documentId}.md");
        _fileContext.Rename(documentPath, newPath);
    }

    private string FormatDocumentPath(string documentId)
    {
        var documentsPath = _variablesService.GetVariable("DOCUMENTS_PATH").AsString();
        return Path.Combine(documentsPath, $"{documentId}.md");
    }
}
