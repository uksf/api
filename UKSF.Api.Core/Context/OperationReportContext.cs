using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

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
