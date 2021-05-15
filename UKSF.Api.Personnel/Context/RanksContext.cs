using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Personnel.Context
{
    public interface IRanksContext : IMongoContext<Rank>, ICachedMongoContext
    {
        new IEnumerable<Rank> Get();
        new Rank GetSingle(string name);
    }

    public class RanksContext : CachedMongoContext<Rank>, IRanksContext
    {
        public RanksContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "ranks") { }

        public override Rank GetSingle(string name)
        {
            return GetSingle(x => x.Name == name);
        }

        protected override void SetCache(IEnumerable<Rank> newCollection)
        {
            lock (LockObject)
            {
                Cache = newCollection?.OrderBy(x => x.Order).ToList();
            }
        }
    }
}
