using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IOperationOrderContext : IMongoContext<DomainOpord>, ICachedMongoContext;

public class OperationOrderContext : CachedMongoContext<DomainOpord>, IOperationOrderContext
{
    public OperationOrderContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "opord"
    ) { }

    protected override IEnumerable<DomainOpord> OrderCollection(IEnumerable<DomainOpord> collection)
    {
        return collection.OrderBy(x => x.Start);
    }
}
