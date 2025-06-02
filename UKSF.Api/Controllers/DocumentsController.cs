using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("docs/folders")]
[Permissions(Permissions.Member)]
public class DocsController(IDocumentFolderService documentFolderService, IDocumentService documentService) : ControllerBase
{
    [HttpGet]
    public List<FolderMetadataResponse> GetAllFolders()
    {
        return documentFolderService.GetAllFolders();
    }

    [HttpGet("{folderId}")]
    public Task<FolderMetadataResponse> GetFolder([FromRoute] string folderId)
    {
        return documentFolderService.GetFolder(folderId);
    }

    [HttpPost]
    public Task<FolderMetadataResponse> CreateFolder([FromBody] CreateFolderRequest createFolder)
    {
        return documentFolderService.CreateFolder(createFolder);
    }

    [HttpPut("{folderId}")]
    public Task<FolderMetadataResponse> UpdateFolder([FromRoute] string folderId, [FromBody] CreateFolderRequest createFolderRequest)
    {
        return documentFolderService.UpdateFolder(folderId, createFolderRequest);
    }

    [HttpDelete("{folderId}")]
    public Task DeleteFolder([FromRoute] string folderId)
    {
        return documentFolderService.DeleteFolder(folderId);
    }

    [HttpGet("{folderId}/documents/{documentId}")]
    public Task<DocumentMetadataResponse> GetDocument([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return documentService.GetDocument(folderId, documentId);
    }

    [HttpPost("{folderId}/documents")]
    public Task<DocumentMetadataResponse> CreateDocument([FromRoute] string folderId, [FromBody] CreateDocumentRequest createDocument)
    {
        return documentService.CreateDocument(folderId, createDocument);
    }

    [HttpPut("{folderId}/documents/{documentId}")]
    public Task<DocumentMetadataResponse> UpdateDocument(
        [FromRoute] string folderId,
        [FromRoute] string documentId,
        [FromBody] CreateDocumentRequest createDocumentRequest
    )
    {
        return documentService.UpdateDocument(folderId, documentId, createDocumentRequest);
    }

    [HttpDelete("{folderId}/documents/{documentId}")]
    public Task DeleteDocument([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return documentService.DeleteDocument(folderId, documentId);
    }

    [HttpGet("{folderId}/documents/{documentId}/content")]
    public Task<DocumentContentResponse> GetDocumentContent([FromRoute] string folderId, [FromRoute] string documentId)
    {
        return documentService.GetDocumentContent(folderId, documentId);
    }

    [HttpPut("{folderId}/documents/{documentId}/content")]
    public Task<DocumentContentResponse> UpdateDocumentContent(
        [FromRoute] string folderId,
        [FromRoute] string documentId,
        [FromBody] UpdateDocumentContentRequest updateDocumentContent
    )
    {
        return documentService.UpdateDocumentContent(folderId, documentId, updateDocumentContent);
    }
}
