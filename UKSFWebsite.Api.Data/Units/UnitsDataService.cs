using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Units;

namespace UKSFWebsite.Api.Data.Units {
    public class UnitsDataService : CachedDataService<Unit>, IUnitsDataService {
        public UnitsDataService(IMongoDatabase database, IDataEventBus dataEventBus) : base(database, dataEventBus, "units") { }

        public override List<Unit> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.order).ToList();
            return Collection;
        }
    }
}
