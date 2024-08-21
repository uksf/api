using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Context;

public interface IDocumentFolderMetadataContext : IMongoContext<DomainDocumentFolderMetadata>;

public class DocumentFolderMetadataContext : MongoContext<DomainDocumentFolderMetadata>, IDocumentFolderMetadataContext
{
    public DocumentFolderMetadataContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(
        mongoCollectionFactory,
        eventBus,
        "documentMetadata"
    ) { }
}
