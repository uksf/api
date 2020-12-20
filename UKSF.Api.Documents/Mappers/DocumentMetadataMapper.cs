using UKSF.Api.Documents.Models;

namespace UKSF.Api.Documents.Mappers {
    public interface IDocumentMetadataMapper {
        DocumentMetadata MapFromContext(ContextDocumentMetadata contextDocumentMetadata, bool canView, bool canEdit);
    }

    public class DocumentMetadataMapper : IDocumentMetadataMapper {
        public DocumentMetadata MapFromContext(ContextDocumentMetadata contextDocumentMetadata, bool canView, bool canEdit) =>
            new() {
                Id = contextDocumentMetadata.Id,
                CreatedUtc = contextDocumentMetadata.CreatedUtc,
                LastUpdatedUtc = contextDocumentMetadata.LastUpdatedUtc,
                CreatorId = contextDocumentMetadata.CreatorId,
                Name = contextDocumentMetadata.Name,
                Path = contextDocumentMetadata.Path,
                CanView = canView,
                CanEdit = canEdit
            };
    }
}
