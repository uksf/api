using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IUnitsContext : IMongoContext<DomainUnit>, ICachedMongoContext;

public class UnitsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainUnit>(mongoCollectionFactory, eventBus, variablesService, "units"), IUnitsContext
{
    protected override IEnumerable<DomainUnit> OrderCollection(IEnumerable<DomainUnit> collection)
    {
        return collection.OrderBy(x => x.Order);
    }
}
