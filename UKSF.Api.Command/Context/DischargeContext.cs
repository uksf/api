using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Command.Context;

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
