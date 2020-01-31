using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Units;

namespace UKSF.Api.Data.Units {
    public class UnitsDataService : CachedDataService<Unit, IUnitsDataService>, IUnitsDataService {
        public UnitsDataService(IDataCollection dataCollection, IDataEventBus<IUnitsDataService> dataEventBus) : base(dataCollection, dataEventBus, "units") { }

        public override List<Unit> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.order).ToList();
            return Collection;
        }
    }
}
