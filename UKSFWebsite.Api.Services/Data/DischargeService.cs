using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Data {
    public class DischargeService : CachedDataService<DischargeCollection>, IDischargeService {
        public DischargeService(IMongoDatabase database) : base(database, "discharges") { }

        public override List<DischargeCollection> Get() {
            return base.Get().OrderByDescending(x => x.discharges.Last().timestamp).ToList();
        }
    }
}
