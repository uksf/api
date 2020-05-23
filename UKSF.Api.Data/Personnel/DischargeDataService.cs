using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class DischargeDataService : CachedDataService<DischargeCollection, IDischargeDataService>, IDischargeDataService {
        public DischargeDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IDischargeDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "discharges") { }

        public override List<DischargeCollection> Get() {
            return base.Get().OrderByDescending(x => x.discharges.Last().timestamp).ToList();
        }
    }
}
