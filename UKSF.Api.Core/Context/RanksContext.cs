using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IRanksContext : IMongoContext<DomainRank>, ICachedMongoContext
{
    new IEnumerable<DomainRank> Get();
    new DomainRank GetSingle(string name);
}

public class RanksContext : CachedMongoContext<DomainRank>, IRanksContext
{
    public RanksContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "ranks"
    ) { }

    protected override IEnumerable<DomainRank> OrderCollection(IEnumerable<DomainRank> collection)
    {
        return collection.OrderBy(x => x.Order);
    }

    public override DomainRank GetSingle(string idOrName)
    {
        return GetSingle(x => x.Id == idOrName || x.Name == idOrName);
    }
}
