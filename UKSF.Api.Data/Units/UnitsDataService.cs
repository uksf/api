using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Units;

namespace UKSF.Api.Data.Units {
    public class UnitsDataService : CachedDataService<Unit, IUnitsDataService>, IUnitsDataService {
        public UnitsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IUnitsDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "units") { }

        public override List<Unit> Get() {
            return base.Get().OrderBy(x => x.order).ToList();
        }

        public override List<Unit> Get(Func<Unit, bool> predicate) {
            return base.Get(predicate).OrderBy(x => x.order).ToList();
        }
    }
}
