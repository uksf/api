using System.Collections.Generic;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Data.Personnel {
    public class RanksDataService : CachedDataService<Rank>, IRanksDataService {
        public RanksDataService(IMongoDatabase database, IDataEventBus dataEventBus) : base(database, dataEventBus, "ranks") { }

        public override List<Rank> Get() {
            base.Get();
            Collection.Sort(Sort);
            return Collection;
        }

        public override Rank GetSingle(string name) => GetSingle(x => x.name == name);

        public int Sort(Rank rankA, Rank rankB) {
            int rankOrderA = rankA?.order ?? 0;
            int rankOrderB = rankB?.order ?? 0;
            return rankOrderA < rankOrderB ? -1 : rankOrderA > rankOrderB ? 1 : 0;
        }
    }
}
