using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Documents.Context;
using UKSF.Api.Documents.Mappers;
using UKSF.Api.Documents.Models;
using UKSF.Api.Documents.Queries;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Documents.Controllers {
    [Route("documents"), Permissions(Permissions.MEMBER)]
    public class DocumentsController : Controller {
        private readonly IDocumentMetadataMapper _documentMetadataMapper;
        private readonly IDocumentsMetadataContext _documentsMetadataContext;
        private readonly IUserPermissionsForDocumentQuery _userPermissionsForDocumentQuery;

        public DocumentsController(
            IDocumentsMetadataContext documentsMetadataContext,
            IUserPermissionsForDocumentQuery userPermissionsForDocumentQuery,
            IDocumentMetadataMapper documentMetadataMapper
        ) {
            _documentsMetadataContext = documentsMetadataContext;
            _userPermissionsForDocumentQuery = userPermissionsForDocumentQuery;
            _documentMetadataMapper = documentMetadataMapper;
        }

        [HttpGet]
        public IEnumerable<DocumentMetadata> GetAllDocuments() {
            IEnumerable<ContextDocumentMetadata> contextDocuments = _documentsMetadataContext.Get();
            return contextDocuments.Select(x => new { document = x, permissions = _userPermissionsForDocumentQuery.Execute(new(x)) })
                                   .Where(x => x.permissions.CanView || x.permissions.CanEdit)
                                   .Select(x => _documentMetadataMapper.MapFromContext(x.document, x.permissions.CanView, x.permissions.CanEdit));
        }

        [HttpGet("{documentId}")]
        public DocumentMetadata GetDocument([FromRoute] string documentId) {
            ContextDocumentMetadata contextDocument = _documentsMetadataContext.GetSingle(documentId);

            if (contextDocument == null) {
                throw new UksfNotFoundException($"Document with id {documentId} not found");
            }

            UserPermissionsForDocumentResult permissions = _userPermissionsForDocumentQuery.Execute(new(contextDocument));
            if (permissions.CanView || permissions.CanEdit) {
                return _documentMetadataMapper.MapFromContext(contextDocument, permissions.CanView, permissions.CanEdit);
            }

            throw new UksfUnauthorizedException();
        }
    }
}
