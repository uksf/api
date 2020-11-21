using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Context {
    public interface IRanksContext : IMongoContext<Rank>, ICachedMongoContext {
        new IEnumerable<Rank> Get();
        new Rank GetSingle(string name);
    }

    public class RanksContext : CachedMongoContext<Rank>, IRanksContext {
        public RanksContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<Rank> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "ranks") { }

        public override Rank GetSingle(string name) => GetSingle(x => x.Name == name);

        protected override void SetCache(IEnumerable<Rank> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.Order).ToList();
            }
        }
    }
}
