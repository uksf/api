using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("docs")]
[Permissions(Permissions.Member)]
public class DocsController : ControllerBase
{
    private readonly IDocumentFolderService _documentFolderService;
    private readonly IDocumentService _documentService;

    public DocsController(IDocumentFolderService documentFolderService, IDocumentService documentService)
    {
        _documentFolderService = documentFolderService;
        _documentService = documentService;
    }

    [HttpGet("folders")]
    public List<FolderMetadataResponse> GetAllFolders()
    {
        return _documentFolderService.GetAllFolders();
    }

    [HttpGet("folders/{folderId}")]
    public Task<FolderMetadataResponse> GetFolder([FromRoute] string folderId)
    {
        return _documentFolderService.GetFolder(folderId);
    }

    [HttpPost("folders")]
    public Task<FolderMetadataResponse> CreateFolder([FromBody] CreateFolderRequest createFolder)
    {
        return _documentFolderService.CreateFolder(createFolder);
    }

    [HttpPut("folders/{folderId}")]
    public Task<FolderMetadataResponse> UpdateFolder([FromRoute] string folderId, [FromBody] CreateFolderRequest createFolderRequest)
    {
        return _documentFolderService.UpdateFolder(folderId, createFolderRequest);
    }

    [HttpDelete("folders/{folderId}")]
    public Task DeleteFolder([FromRoute] string folderId)
    {
        return _documentFolderService.DeleteFolder(folderId);
    }

    [HttpGet("folders/{folderId}/documents/{documentId}")]
    public Task<DocumentMetadataResponse> GetDocument([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return _documentService.GetDocument(folderId, documentId);
    }

    [HttpPost("folders/{folderId}/documents")]
    public Task<DocumentMetadataResponse> CreateDocument([FromRoute] string folderId, [FromBody] CreateDocumentRequest createDocument)
    {
        return _documentService.CreateDocument(folderId, createDocument);
    }

    [HttpPut("folders/{folderId}/documents/{documentId}")]
    public Task<DocumentMetadataResponse> UpdateDocument(
        [FromRoute] string folderId,
        [FromRoute] string documentId,
        [FromBody] CreateDocumentRequest createDocumentRequest
    )
    {
        return _documentService.UpdateDocument(folderId, documentId, createDocumentRequest);
    }

    [HttpDelete("folders/{folderId}/documents/{documentId}")]
    public Task DeleteDocument([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return _documentService.DeleteDocument(folderId, documentId);
    }

    [HttpGet("folders/{folderId}/documents/{documentId}/content")]
    public Task<DocumentContentResponse> GetDocumentContent([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return _documentService.GetDocumentContent(folderId, documentId);
    }

    [HttpPut("folders/{folderId}/documents/{documentId}/content")]
    public Task<DocumentContentResponse> UpdateDocumentContent(
        [FromRoute] string folderId,
        [FromRoute] string documentId,
        [FromBody] UpdateDocumentContentRequest updateDocumentContent
    )
    {
        return _documentService.UpdateDocumentContent(folderId, documentId, updateDocumentContent);
    }
}
