using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Data.Personnel {
    public class RanksDataService : CachedDataService<Rank, IRanksDataService>, IRanksDataService {
        public RanksDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IRanksDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "ranks") { }

        public override List<Rank> Get() {
            return base.Get().OrderBy(x => x.order).ToList();
        }

        public override Rank GetSingle(string name) => GetSingle(x => x.name == name);
    }
}
