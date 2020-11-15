using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Context {
    public interface IRanksDataService : IDataService<Rank>, ICachedDataService {
        new IEnumerable<Rank> Get();
        new Rank GetSingle(string name);
    }

    public class RanksDataService : CachedDataService<Rank>, IRanksDataService {
        public RanksDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Rank> dataEventBus) : base(dataCollectionFactory, dataEventBus, "ranks") { }

        protected override void SetCache(IEnumerable<Rank> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.order).ToList();
            }
        }

        public override Rank GetSingle(string name) => GetSingle(x => x.name == name);
    }
}
