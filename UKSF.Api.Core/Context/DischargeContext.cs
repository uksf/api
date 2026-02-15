using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IDischargeContext : IMongoContext<DomainDischargeCollection>, ICachedMongoContext;

public class DischargeContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainDischargeCollection>(mongoCollectionFactory, eventBus, variablesService, "discharges"), IDischargeContext
{
    protected override IEnumerable<DomainDischargeCollection> OrderCollection(IEnumerable<DomainDischargeCollection> collection)
    {
        return collection.OrderByDescending(x => x.Discharges.LastOrDefault()?.Timestamp ?? DateTime.MinValue);
    }
}
