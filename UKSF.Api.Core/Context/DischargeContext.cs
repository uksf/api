using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IDischargeContext : IMongoContext<DischargeCollection>, ICachedMongoContext { }

public class DischargeContext : CachedMongoContext<DischargeCollection>, IDischargeContext
{
    public DischargeContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "discharges"
    ) { }

    public override IEnumerable<DischargeCollection> Get()
    {
        return base.Get().OrderByDescending(x => x.Discharges.Last().Timestamp);
    }

    public override IEnumerable<DischargeCollection> Get(Func<DischargeCollection, bool> predicate)
    {
        return base.Get(predicate).OrderByDescending(x => x.Discharges.Last().Timestamp);
    }
}
