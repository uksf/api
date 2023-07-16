using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IRolesContext : IMongoContext<DomainRole>, ICachedMongoContext
{
    new IEnumerable<DomainRole> Get();
    new DomainRole GetSingle(string name);
}

public class RolesContext : CachedMongoContext<DomainRole>, IRolesContext
{
    public RolesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "roles"
    ) { }

    public override DomainRole GetSingle(string idOrName)
    {
        return GetSingle(x => x.Id == idOrName || x.Name == idOrName);
    }

    public override IEnumerable<DomainRole> Get()
    {
        return base.Get().OrderBy(x => x.Name);
    }

    public override IEnumerable<DomainRole> Get(Func<DomainRole, bool> predicate)
    {
        return base.Get(predicate).OrderBy(x => x.Name);
    }
}
