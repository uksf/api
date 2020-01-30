using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Units;

namespace UKSF.Api.Data.Units {
    public class UnitsDataService : CachedDataService<Unit, IUnitsDataService>, IUnitsDataService {
        public UnitsDataService(IMongoDatabase database, IDataEventBus<IUnitsDataService> dataEventBus) : base(database, dataEventBus, "units") { }

        public override List<Unit> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.order).ToList();
            return Collection;
        }
    }
}
