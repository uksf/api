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

    public override IEnumerable<DomainUnit> Get()
    {
        return base.Get().OrderBy(x => x.Order);
    }

    public override IEnumerable<DomainUnit> Get(Func<DomainUnit, bool> predicate)
    {
        return base.Get(predicate).OrderBy(x => x.Order);
    }
}
