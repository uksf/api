using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IDischargeContext : IMongoContext<DomainDischargeCollection>, ICachedMongoContext;

public class DischargeContext : CachedMongoContext<DomainDischargeCollection>, IDischargeContext
{
    public DischargeContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "discharges"
    ) { }

    protected override IEnumerable<DomainDischargeCollection> OrderCollection(IEnumerable<DomainDischargeCollection> collection)
    {
        return collection.OrderByDescending(x => x.Discharges.Last().Timestamp);
    }
}
