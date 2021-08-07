using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Personnel.Context
{
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
}
