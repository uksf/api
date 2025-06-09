using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IDocumentFolderMetadataContext : IMongoContext<DomainDocumentFolderMetadata>, ICachedMongoContext;

public class DocumentFolderMetadataContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainDocumentFolderMetadata>(mongoCollectionFactory, eventBus, variablesService, "documentMetadata"), IDocumentFolderMetadataContext
{
    protected override IEnumerable<DomainDocumentFolderMetadata> OrderCollection(IEnumerable<DomainDocumentFolderMetadata> collection)
    {
        return collection.OrderBy(x => x.FullPath);
    }
}
