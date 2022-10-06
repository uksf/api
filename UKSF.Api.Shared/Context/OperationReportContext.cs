using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface IOperationReportContext : IMongoContext<Oprep>, ICachedMongoContext { }

public class OperationReportContext : CachedMongoContext<Oprep>, IOperationReportContext
{
    public OperationReportContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "oprep") { }

    protected override void SetCache(IEnumerable<Oprep> newCollection)
    {
        lock (LockObject)
        {
            Cache = newCollection?.OrderBy(x => x.Start).ToList();
        }
    }
}
