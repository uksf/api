using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IOperationOrderContext : IMongoContext<DomainOpord>, ICachedMongoContext;

public class OperationOrderContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainOpord>(mongoCollectionFactory, eventBus, variablesService, "opord"), IOperationOrderContext
{
    protected override IEnumerable<DomainOpord> OrderCollection(IEnumerable<DomainOpord> collection)
    {
        return collection.OrderBy(x => x.Start);
    }
}
