using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Data.Personnel {
    public class DischargeDataService : CachedDataService<DischargeCollection>, IDischargeDataService {
        public DischargeDataService(IMongoDatabase database, IDataEventBus dataEventBus) : base(database, dataEventBus, "discharges") { }

        public override List<DischargeCollection> Get() {
            return base.Get().OrderByDescending(x => x.discharges.Last().timestamp).ToList();
        }
    }
}
