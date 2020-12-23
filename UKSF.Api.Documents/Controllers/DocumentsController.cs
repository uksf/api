using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Documents.Commands;
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
        private readonly IVerifyDocumentPermissionsCommand _verifyDocumentPermissionsCommand;
        private readonly IDocumentsMetadataContext _documentsMetadataContext;
        private readonly IUserPermissionsForDocumentQuery _userPermissionsForDocumentQuery;

        public DocumentsController(
            IDocumentsMetadataContext documentsMetadataContext,
            IUserPermissionsForDocumentQuery userPermissionsForDocumentQuery,
            IDocumentMetadataMapper documentMetadataMapper,
            IVerifyDocumentPermissionsCommand verifyDocumentPermissionsCommand
        ) {
            _documentsMetadataContext = documentsMetadataContext;
            _userPermissionsForDocumentQuery = userPermissionsForDocumentQuery;
            _documentMetadataMapper = documentMetadataMapper;
            _verifyDocumentPermissionsCommand = verifyDocumentPermissionsCommand;
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

            UserPermissionsForDocumentResult permissionsResult = _userPermissionsForDocumentQuery.Execute(new(contextDocument));
            if (permissionsResult.CanView || permissionsResult.CanEdit) {
                return _documentMetadataMapper.MapFromContext(contextDocument, permissionsResult.CanView, permissionsResult.CanEdit);
            }

            throw new UksfUnauthorizedException();
        }

        [HttpPut("{documentId}")]
        public async Task<DocumentPermissions> SetDocumentPermissions([FromRoute] string documentId, [FromBody] DocumentPermissions permissions) {
            ContextDocumentMetadata contextDocument = _documentsMetadataContext.GetSingle(documentId);

            if (contextDocument == null) {
                throw new UksfNotFoundException($"Document with id {documentId} not found");
            }

            UserPermissionsForDocumentResult permissionsResult = _userPermissionsForDocumentQuery.Execute(new(contextDocument));
            if (!permissionsResult.CanEdit) {
                throw new UksfUnauthorizedException();
            }

            _verifyDocumentPermissionsCommand.Execute(new(contextDocument, permissions));

            await _documentsMetadataContext.Update(documentId, x => x.Permissions, permissions);
            return _documentsMetadataContext.GetSingle(documentId).Permissions;
        }
    }
}
