using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

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
