using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

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
