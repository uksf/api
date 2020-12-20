using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Documents.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Documents.Context {
    public interface IDocumentsMetadataContext : IMongoContext<ContextDocumentMetadata> { }

    public class DocumentsMetadataContext : MongoContext<ContextDocumentMetadata>, IDocumentsMetadataContext {
        public DocumentsMetadataContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "documentsMetadata") { }
    }
}
