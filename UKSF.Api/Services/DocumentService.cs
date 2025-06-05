using MongoDB.Driver;
using MongoDB.Driver.Linq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;

namespace UKSF.Api.Services;

public interface IDocumentService
{
    Task<DocumentMetadataResponse> GetDocument(string folderId, string documentId);
    Task<DocumentMetadataResponse> CreateDocument(string folderId, CreateDocumentRequest createDocument);
    Task<DocumentMetadataResponse> UpdateDocument(string folderId, string documentId, CreateDocumentRequest newPermissions);
    Task DeleteDocument(string folderId, string documentId);

    Task<DocumentContentResponse> GetDocumentContent(string folderId, string documentId);
    Task<DocumentContentResponse> UpdateDocumentContent(string folderId, string documentId, UpdateDocumentContentRequest updateDocumentContent);
}

public class DocumentService(
    IDocumentFolderMetadataContext documentFolderMetadataContext,
    IHttpContextService httpContextService,
    IRoleBasedDocumentPermissionsService roleBasedDocumentPermissionsService,
    IVariablesService variablesService,
    IFileContext fileContext,
    IClock clock,
    IUksfLogger logger
) : IDocumentService
{
    public Task<DocumentMetadataResponse> GetDocument(string folderId, string documentId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId);

        return Task.FromResult(MapDocument(documentMetadata));
    }

    public async Task<DocumentMetadataResponse> CreateDocument(string folderId, CreateDocumentRequest createDocument)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        if (!roleBasedDocumentPermissionsService.DoesContextHaveWritePermission(folderMetadata))
        {
            throw new FolderException("Cannot create documents in this folder");
        }

        var documentMetadata = new DomainDocumentMetadata
        {
            Folder = folderId,
            Name = createDocument.Name,
            FullPath = Path.Combine(folderMetadata.FullPath, createDocument.Name),
            Created = clock.UtcNow(),
            LastUpdated = clock.UtcNow(),
            Creator = httpContextService.GetUserId(),
            // Legacy permissions (for backwards compatibility)
            ReadPermissions = createDocument.ReadPermissions,
            WritePermissions = createDocument.WritePermissions,
            // New role-based permissions
            Owner = createDocument.Owner ?? httpContextService.GetUserId(),
            RoleBasedPermissions = createDocument.RoleBasedPermissions
        };

        if (!roleBasedDocumentPermissionsService.DoesContextHaveReadPermission(documentMetadata))
        {
            throw new DocumentException("Cannot create document you won't be able to view");
        }

        if (folderMetadata.Documents.Any(x => x.Name.EqualsIgnoreCase(documentMetadata.Name)))
        {
            throw new DocumentException($"A document already exists at path '{documentMetadata.FullPath}'");
        }

        await documentFolderMetadataContext.Update(folderId, Builders<DomainDocumentFolderMetadata>.Update.Push(x => x.Documents, documentMetadata));
        CreateDocumentFile(documentMetadata.Id);

        logger.LogAudit($"Created document at {documentMetadata.FullPath}");
        return MapDocument(documentMetadata);
    }

    public async Task<DocumentMetadataResponse> UpdateDocument(string folderId, string documentId, CreateDocumentRequest newPermissions)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        if (!roleBasedDocumentPermissionsService.DoesContextHaveWritePermission(folderMetadata))
        {
            throw new FolderException($"Cannot edit documents in this folder '{folderMetadata.Name}'");
        }

        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId);
        if (!roleBasedDocumentPermissionsService.DoesContextHaveWritePermission(documentMetadata))
        {
            throw new DocumentException($"Cannot edit document '{folderMetadata.Name}/{documentMetadata.Name}'");
        }

        // Calculate new full path
        var newFullPath = Path.Combine(folderMetadata.FullPath, newPermissions.Name);

        // Check for name collision
        if (folderMetadata.Documents.Any(x => x.Id != documentId && x.Name.EqualsIgnoreCase(newPermissions.Name)))
        {
            throw new DocumentException($"A document already exists at path '{newFullPath}'");
        }

        // Update both legacy and new permission fields, including FullPath
        var updates = new List<UpdateDefinition<DomainDocumentFolderMetadata>>
        {
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Documents.FirstMatchingElement().Name, newPermissions.Name),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Documents.FirstMatchingElement().FullPath, newFullPath),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Documents.FirstMatchingElement().WritePermissions, newPermissions.WritePermissions),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Documents.FirstMatchingElement().ReadPermissions, newPermissions.ReadPermissions),
            Builders<DomainDocumentFolderMetadata>.Update.Set(
                x => x.Documents.FirstMatchingElement().Owner,
                newPermissions.Owner ?? documentMetadata.Owner
            ),
            Builders<DomainDocumentFolderMetadata>.Update.Set(
                x => x.Documents.FirstMatchingElement().RoleBasedPermissions,
                newPermissions.RoleBasedPermissions
            )
        };

        var combinedUpdate = Builders<DomainDocumentFolderMetadata>.Update.Combine(updates);
        await documentFolderMetadataContext.FindAndUpdate(x => x.Id == folderId && x.Documents.Any(y => y.Id == documentId), combinedUpdate);

        logger.LogAudit($"Updated document for {newFullPath}");
        documentMetadata = ValidateAndGetDocument(folderMetadata, documentId);
        return MapDocument(documentMetadata);
    }

    public async Task DeleteDocument(string folderId, string documentId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        if (!roleBasedDocumentPermissionsService.DoesContextHaveWritePermission(folderMetadata))
        {
            throw new FolderException($"Cannot delete documents from this folder '{folderMetadata.Name}'");
        }

        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId);
        if (!roleBasedDocumentPermissionsService.DoesContextHaveWritePermission(documentMetadata))
        {
            throw new DocumentException($"Cannot delete document '{folderMetadata.Name}/{documentMetadata.Name}'");
        }

        ValidateAndGetDocumentPath(documentMetadata.Id);

        await documentFolderMetadataContext.Update(
            folderId,
            Builders<DomainDocumentFolderMetadata>.Update.PullFilter(x => x.Documents, x => x.Id == documentMetadata.Id)
        );
        RenameDocumentFile(documentId);

        logger.LogAudit($"Deleted document at {documentMetadata.FullPath}");
    }

    public async Task<DocumentContentResponse> GetDocumentContent(string folderId, string documentId)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId);
        var documentPath = ValidateAndGetDocumentPath(documentMetadata.Id);
        var text = await fileContext.ReadAllText(documentPath);
        return new DocumentContentResponse { Text = text, LastUpdated = documentMetadata.LastUpdated };
    }

    public async Task<DocumentContentResponse> UpdateDocumentContent(string folderId, string documentId, UpdateDocumentContentRequest updateDocumentContent)
    {
        var folderMetadata = ValidateAndGetFolder(folderId);
        if (!roleBasedDocumentPermissionsService.DoesContextHaveWritePermission(folderMetadata))
        {
            throw new FolderException($"Cannot edit documents in this folder '{folderMetadata.Name}'");
        }

        var documentMetadata = ValidateAndGetDocument(folderMetadata, documentId);
        if (!roleBasedDocumentPermissionsService.DoesContextHaveWritePermission(documentMetadata))
        {
            throw new DocumentException($"Cannot edit document '{folderMetadata.Name}/{documentMetadata.Name}'");
        }

        if (updateDocumentContent.LastKnownUpdated < documentMetadata.LastUpdated)
        {
            throw new DocumentException($"Document update for '{documentMetadata.Name}' is behind more recent changes. Please refresh");
        }

        var updated = clock.UtcNow();
        await documentFolderMetadataContext.FindAndUpdate(
            x => x.Id == folderId && x.Documents.Any(y => y.Id == documentId),
            Builders<DomainDocumentFolderMetadata>.Update.Set(x => x.Documents.FirstMatchingElement().LastUpdated, updated)
        );

        var documentPath = ValidateAndGetDocumentPath(documentMetadata.Id);
        await fileContext.WriteTextToFile(documentPath, updateDocumentContent.NewText);

        logger.LogAudit($"Updated document at {documentMetadata.FullPath}");
        return new DocumentContentResponse { Text = updateDocumentContent.NewText, LastUpdated = updated };
    }

    private DomainDocumentFolderMetadata ValidateAndGetFolder(string folderId)
    {
        var folderMetadata = documentFolderMetadataContext.GetSingle(folderId);
        if (folderMetadata == null)
        {
            throw new FolderNotFoundException($"Folder with ID '{folderId}' not found");
        }

        if (!roleBasedDocumentPermissionsService.DoesContextHaveReadPermission(folderMetadata))
        {
            throw new FolderException($"Cannot view folder '{folderMetadata.Name}'");
        }

        return folderMetadata;
    }

    private DomainDocumentMetadata ValidateAndGetDocument(DomainDocumentFolderMetadata folderMetadata, string documentId)
    {
        var documentMetadata = folderMetadata.Documents.FirstOrDefault(x => x.Id == documentId);
        if (documentMetadata == null)
        {
            throw new DocumentNotFoundException($"Document with ID '{documentId}' not found");
        }

        if (!roleBasedDocumentPermissionsService.DoesContextHaveReadPermission(documentMetadata))
        {
            throw new DocumentException($"Cannot view document '{folderMetadata.Name}/{documentMetadata.Name}'");
        }

        return documentMetadata;
    }

    private string ValidateAndGetDocumentPath(string documentId)
    {
        var documentPath = FormatDocumentPath(documentId);
        if (!fileContext.Exists(documentPath))
        {
            throw new DocumentNotFoundException("No document file found");
        }

        return documentPath;
    }

    private void CreateDocumentFile(string documentId)
    {
        var documentPath = FormatDocumentPath(documentId);
        fileContext.CreateFile(documentPath);
    }

    private void RenameDocumentFile(string documentId)
    {
        var documentsPath = variablesService.GetVariable("DOCUMENTS_PATH").AsString();
        var documentPath = Path.Combine(documentsPath, $"{documentId}.json");
        var newPath = Path.Combine(documentsPath, "__DELETED", $"{documentId}.json");
        fileContext.Rename(documentPath, newPath);
    }

    private string FormatDocumentPath(string documentId)
    {
        var documentsPath = variablesService.GetVariable("DOCUMENTS_PATH").AsString();
        return Path.Combine(documentsPath, $"{documentId}.json");
    }

    private DocumentMetadataResponse MapDocument(DomainDocumentMetadata documentMetadata)
    {
        var effectivePermissions = roleBasedDocumentPermissionsService.GetEffectivePermissions(documentMetadata);
        var inheritedPermissions = roleBasedDocumentPermissionsService.GetInheritedPermissions(documentMetadata);

        return new DocumentMetadataResponse
        {
            Id = documentMetadata.Id,
            Folder = documentMetadata.Folder,
            Name = documentMetadata.Name,
            FullPath = documentMetadata.FullPath,
            Created = documentMetadata.Created,
            LastUpdated = documentMetadata.LastUpdated,
            Creator = documentMetadata.Creator,
            // Legacy permissions (keep for backwards compatibility)
            ReadPermissions = documentMetadata.ReadPermissions,
            WritePermissions = documentMetadata.WritePermissions,
            // NEW: Role-based permissions
            Owner = documentMetadata.Owner,
            RoleBasedPermissions = documentMetadata.RoleBasedPermissions,
            EffectivePermissions = effectivePermissions,
            InheritedPermissions = inheritedPermissions,
            CanWrite = roleBasedDocumentPermissionsService.DoesContextHaveWritePermission(documentMetadata)
        };
    }
}
