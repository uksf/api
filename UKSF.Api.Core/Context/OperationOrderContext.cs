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

    public override IEnumerable<Opord> Get()
    {
        return base.Get().OrderBy(x => x.Start);
    }

    public override IEnumerable<Opord> Get(Func<Opord, bool> predicate)
    {
        return base.Get(predicate).OrderBy(x => x.Start);
    }
}
