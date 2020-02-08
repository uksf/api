using System.Collections.Generic;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Data.Personnel {
    public class RanksDataService : CachedDataService<Rank, IRanksDataService>, IRanksDataService {
        public RanksDataService(IDataCollection dataCollection, IDataEventBus<IRanksDataService> dataEventBus) : base(dataCollection, dataEventBus, "ranks") { }

        public override List<Rank> Get() {
            base.Get();
            Collection.Sort(RankUtilities.Sort);
            return Collection;
        }

        public override Rank GetSingle(string name) => GetSingle(x => x.name == name);
    }
}
