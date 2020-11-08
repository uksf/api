using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Context {
    public interface IUnitsDataService : IDataService<Unit>, ICachedDataService { }

    public class UnitsDataService : CachedDataService<Unit>, IUnitsDataService {
        public UnitsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Unit> dataEventBus) : base(dataCollectionFactory, dataEventBus, "units") { }

        protected override void SetCache(IEnumerable<Unit> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.order).ToList();
            }
        }
    }
}
