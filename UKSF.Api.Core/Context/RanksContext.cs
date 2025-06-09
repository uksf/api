using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IRanksContext : IMongoContext<DomainRank>, ICachedMongoContext
{
    new IEnumerable<DomainRank> Get();
    new DomainRank GetSingle(string name);
}

public class RanksContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainRank>(mongoCollectionFactory, eventBus, variablesService, "ranks"), IRanksContext
{
    public override DomainRank GetSingle(string idOrName)
    {
        return GetSingle(x => x.Id == idOrName || x.Name == idOrName);
    }

    protected override IEnumerable<DomainRank> OrderCollection(IEnumerable<DomainRank> collection)
    {
        return collection.OrderBy(x => x.Order);
    }
}
