using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Command.Context;

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
