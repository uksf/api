using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class DischargeDataService : CachedDataService<DischargeCollection>, IDischargeDataService {
        public DischargeDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<DischargeCollection> dataEventBus) : base(dataCollectionFactory, dataEventBus, "discharges") { }

        protected override void SetCache(IEnumerable<DischargeCollection> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderByDescending(x => x.discharges.Last().timestamp).ToList();
            }
        }
    }
}
