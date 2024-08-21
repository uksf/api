using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IRolesContext : IMongoContext<DomainRole>, ICachedMongoContext
{
    new IEnumerable<DomainRole> Get();
    new DomainRole GetSingle(string name);
}

public class RolesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainRole>(mongoCollectionFactory, eventBus, variablesService, "roles"), IRolesContext
{
    protected override IEnumerable<DomainRole> OrderCollection(IEnumerable<DomainRole> collection)
    {
        return collection.OrderBy(x => x.Name);
    }

    public override DomainRole GetSingle(string idOrName)
    {
        return GetSingle(x => x.Id == idOrName || x.Name == idOrName);
    }
}
