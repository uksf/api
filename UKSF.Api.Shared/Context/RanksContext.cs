using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface IRanksContext : IMongoContext<DomainRank>, ICachedMongoContext
{
    new IEnumerable<DomainRank> Get();
    new DomainRank GetSingle(string name);
}

public class RanksContext : CachedMongoContext<DomainRank>, IRanksContext
{
    public RanksContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "ranks") { }

    public override DomainRank GetSingle(string name)
    {
        return GetSingle(x => x.Name == name);
    }

    protected override void SetCache(IEnumerable<DomainRank> newCollection)
    {
        lock (LockObject)
        {
            Cache = newCollection?.OrderBy(x => x.Order).ToList();
        }
    }
}
