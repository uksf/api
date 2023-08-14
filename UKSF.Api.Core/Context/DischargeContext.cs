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

    protected override IEnumerable<DischargeCollection> OrderCollection(IEnumerable<DischargeCollection> collection)
    {
        return collection.OrderByDescending(x => x.Discharges.Last().Timestamp);
    }
}
