using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface IOperationOrderContext : IMongoContext<Opord>, ICachedMongoContext { }

public class OperationOrderContext : CachedMongoContext<Opord>, IOperationOrderContext
{
    public OperationOrderContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "opord") { }

    protected override void SetCache(IEnumerable<Opord> newCollection)
    {
        lock (LockObject)
        {
            Cache = newCollection?.OrderBy(x => x.Start).ToList();
        }
    }
}
