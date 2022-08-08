using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Personnel.Context;

public interface IRolesContext : IMongoContext<DomainRole>, ICachedMongoContext
{
    new IEnumerable<DomainRole> Get();
    new DomainRole GetSingle(string name);
}

public class RolesContext : CachedMongoContext<DomainRole>, IRolesContext
{
    public RolesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "roles") { }

    public override DomainRole GetSingle(string name)
    {
        return GetSingle(x => x.Name == name);
    }

    protected override void SetCache(IEnumerable<DomainRole> newCollection)
    {
        lock (LockObject)
        {
            Cache = newCollection?.OrderBy(x => x.Name).ToList();
        }
    }
}
