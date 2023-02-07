using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

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
