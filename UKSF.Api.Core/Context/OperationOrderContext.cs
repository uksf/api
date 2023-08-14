using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IOperationOrderContext : IMongoContext<Opord>, ICachedMongoContext { }

public class OperationOrderContext : CachedMongoContext<Opord>, IOperationOrderContext
{
    public OperationOrderContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "opord"
    ) { }

    protected override IEnumerable<Opord> OrderCollection(IEnumerable<Opord> collection)
    {
        return collection.OrderBy(x => x.Start);
    }
}
