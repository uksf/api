using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface IUnitsContext : IMongoContext<DomainUnit>, ICachedMongoContext { }

public class UnitsContext : CachedMongoContext<DomainUnit>, IUnitsContext
{
    public UnitsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "units") { }

    protected override void SetCache(IEnumerable<DomainUnit> newCollection)
    {
        lock (LockObject)
        {
            Cache = newCollection?.OrderBy(x => x.Order).ToList();
        }
    }
}
