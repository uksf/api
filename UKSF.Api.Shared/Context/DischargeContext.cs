using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

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
