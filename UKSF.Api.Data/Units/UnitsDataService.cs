using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Units;

namespace UKSF.Api.Data.Units {
    public class UnitsDataService : CachedDataService<Unit>, IUnitsDataService {
        public UnitsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Unit> dataEventBus) : base(dataCollectionFactory, dataEventBus, "units") { }

        protected override void SetCache(IEnumerable<Unit> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.order).ToList();
            }
        }
    }
}
