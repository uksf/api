using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("docs")]
[Permissions(Permissions.Member)]
public class DocsController : ControllerBase
{
    private readonly IDocumentFolderService _documentFolderService;
    private readonly IDocumentService _documentsService;

    public DocsController(IDocumentFolderService documentFolderService, IDocumentService documentsService)
    {
        _documentFolderService = documentFolderService;
        _documentsService = documentsService;
    }

    [HttpGet("folders")]
    public List<DomainDocumentFolderMetadata> GetAllFolders()
    {
        return _documentFolderService.GetAllFolders();
    }

    [HttpGet("folders/{folderId}")]
    public Task<DomainDocumentFolderMetadata> GetFolder([FromRoute] string folderId)
    {
        return _documentFolderService.GetFolder(folderId);
    }

    [HttpPost("folders")]
    public Task<DomainDocumentFolderMetadata> CreateFolder([FromBody] CreateFolderRequest createFolder)
    {
        return _documentFolderService.CreateFolder(createFolder);
    }

    [HttpPut("folders/{folderId}/permissions")]
    public Task<DomainDocumentFolderMetadata> UpdateFolderPermissions([FromRoute] string folderId, [FromBody] UpdateDocumentPermissionsRequest newPermissions)
    {
        return _documentFolderService.UpdateFolderPermissions(folderId, newPermissions);
    }

    [HttpDelete("folders/{folderId}")]
    public Task DeleteFolder([FromRoute] string folderId)
    {
        return _documentFolderService.DeleteFolder(folderId);
    }

    [HttpGet("folders/{folderId}/documents/{documentId}")]
    public Task<DomainDocumentMetadata> GetDocument([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return _documentsService.GetDocument(folderId, documentId);
    }

    [HttpPost("folders/{folderId}/documents")]
    public Task<DomainDocumentMetadata> CreateDocument([FromRoute] string folderId, [FromBody] CreateDocumentRequest createDocument)
    {
        return _documentsService.CreateDocument(folderId, createDocument);
    }

    [HttpPut("folders/{folderId}/documents/{documentId}/permissions")]
    public Task<DomainDocumentMetadata> UpdateDocumentPermissions(
        [FromRoute] string folderId,
        [FromRoute] string documentId,
        [FromBody] UpdateDocumentPermissionsRequest newPermissions
    )
    {
        return _documentsService.UpdateDocumentPermissions(folderId, documentId, newPermissions);
    }

    [HttpDelete("folders/{folderId}/documents/{documentId}")]
    public Task DeleteDocument([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return _documentsService.DeleteDocument(folderId, documentId);
    }

    [HttpGet("folders/{folderId}/documents/{documentId}/content")]
    public Task<DocumentContentResponse> GetDocumentContent([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return _documentsService.GetDocumentContent(folderId, documentId);
    }

    [HttpPut("folders/{folderId}/documents/{documentId}/content")]
    public Task<DocumentContentResponse> UpdateDocumentContent(
        [FromRoute] string folderId,
        [FromRoute] string documentId,
        [FromBody] UpdateDocumentContentRequest updateDocumentContent
    )
    {
        return _documentsService.UpdateDocumentContent(folderId, documentId, updateDocumentContent);
    }
}
