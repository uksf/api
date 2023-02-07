using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

public interface IDischargeContext : IMongoContext<DischargeCollection>, ICachedMongoContext { }

public class DischargeContext : CachedMongoContext<DischargeCollection>, IDischargeContext
{
    public DischargeContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "discharges") { }

    protected override void SetCache(IEnumerable<DischargeCollection> newCollection)
    {
        lock (LockObject)
        {
            Cache = newCollection?.OrderByDescending(x => x.Discharges.Last().Timestamp).ToList();
        }
    }
}
