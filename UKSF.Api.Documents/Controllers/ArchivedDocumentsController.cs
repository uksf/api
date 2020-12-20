using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Documents.Context;
using UKSF.Api.Documents.Models;
using UKSF.Api.Shared;

namespace UKSF.Api.Documents.Controllers {
    [Route("documents/archived"), Permissions(Permissions.ADMIN)]
    public class ArchivedDocumentsController {
        private readonly IArchivedDocumentsMetadataContext _archivedDocumentsMetadataContext;

        public ArchivedDocumentsController(IArchivedDocumentsMetadataContext archivedDocumentsMetadataContext) => _archivedDocumentsMetadataContext = archivedDocumentsMetadataContext;

        [HttpGet]
        public IEnumerable<ContextDocumentMetadata> GetAllArchivedDocuments() => _archivedDocumentsMetadataContext.Get();

        [HttpGet("{documentId}")]
        public ContextDocumentMetadata GetArchivedDocument([FromRoute] string documentId) => _archivedDocumentsMetadataContext.GetSingle(documentId);
    }
}
