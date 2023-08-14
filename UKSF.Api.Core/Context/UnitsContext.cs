using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IUnitsContext : IMongoContext<DomainUnit>, ICachedMongoContext { }

public class UnitsContext : CachedMongoContext<DomainUnit>, IUnitsContext
{
    public UnitsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "units"
    ) { }

    protected override IEnumerable<DomainUnit> OrderCollection(IEnumerable<DomainUnit> collection)
    {
        return collection.OrderBy(x => x.Order);
    }
}
