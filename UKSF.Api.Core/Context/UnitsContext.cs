using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

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
