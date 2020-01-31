using System.Collections.Generic;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class RanksDataService : CachedDataService<Rank, IRanksDataService>, IRanksDataService {
        public RanksDataService(IDataCollection dataCollection, IDataEventBus<IRanksDataService> dataEventBus) : base(dataCollection, dataEventBus, "ranks") { }

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
