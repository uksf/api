using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Documents.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Documents.Context {
    public interface IArchivedDocumentsMetadataContext : IMongoContext<ContextDocumentMetadata> { }

    public class ArchivedDocumentsMetadataContext : MongoContext<ContextDocumentMetadata>, IArchivedDocumentsMetadataContext {
        public ArchivedDocumentsMetadataContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "archivedDocumentsMetadata") { }

        protected override void DataEvent(EventModel dataModel) { }
    }
}
