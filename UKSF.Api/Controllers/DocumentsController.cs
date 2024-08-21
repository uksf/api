using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("docs")]
[Permissions(Permissions.Member)]
public class DocsController(IDocumentFolderService documentFolderService, IDocumentService documentService) : ControllerBase
{
    [HttpGet("folders")]
    public List<FolderMetadataResponse> GetAllFolders()
    {
        return documentFolderService.GetAllFolders();
    }

    [HttpGet("folders/{folderId}")]
    public Task<FolderMetadataResponse> GetFolder([FromRoute] string folderId)
    {
        return documentFolderService.GetFolder(folderId);
    }

    [HttpPost("folders")]
    public Task<FolderMetadataResponse> CreateFolder([FromBody] CreateFolderRequest createFolder)
    {
        return documentFolderService.CreateFolder(createFolder);
    }

    [HttpPut("folders/{folderId}")]
    public Task<FolderMetadataResponse> UpdateFolder([FromRoute] string folderId, [FromBody] CreateFolderRequest createFolderRequest)
    {
        return documentFolderService.UpdateFolder(folderId, createFolderRequest);
    }

    [HttpDelete("folders/{folderId}")]
    public Task DeleteFolder([FromRoute] string folderId)
    {
        return documentFolderService.DeleteFolder(folderId);
    }

    [HttpGet("folders/{folderId}/documents/{documentId}")]
    public Task<DocumentMetadataResponse> GetDocument([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return documentService.GetDocument(folderId, documentId);
    }

    [HttpPost("folders/{folderId}/documents")]
    public Task<DocumentMetadataResponse> CreateDocument([FromRoute] string folderId, [FromBody] CreateDocumentRequest createDocument)
    {
        return documentService.CreateDocument(folderId, createDocument);
    }

    [HttpPut("folders/{folderId}/documents/{documentId}")]
    public Task<DocumentMetadataResponse> UpdateDocument(
        [FromRoute] string folderId,
        [FromRoute] string documentId,
        [FromBody] CreateDocumentRequest createDocumentRequest
    )
    {
        return documentService.UpdateDocument(folderId, documentId, createDocumentRequest);
    }

    [HttpDelete("folders/{folderId}/documents/{documentId}")]
    public Task DeleteDocument([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return documentService.DeleteDocument(folderId, documentId);
    }

    [HttpGet("folders/{folderId}/documents/{documentId}/content")]
    public Task<DocumentContentResponse> GetDocumentContent([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return documentService.GetDocumentContent(folderId, documentId);
    }

    [HttpPut("folders/{folderId}/documents/{documentId}/content")]
    public Task<DocumentContentResponse> UpdateDocumentContent(
        [FromRoute] string folderId,
        [FromRoute] string documentId,
        [FromBody] UpdateDocumentContentRequest updateDocumentContent
    )
    {
        return documentService.UpdateDocumentContent(folderId, documentId, updateDocumentContent);
    }
}
