using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationOrderDataService : CachedDataService<Opord>, IOperationOrderDataService {
        public OperationOrderDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Opord> dataEventBus) : base(dataCollectionFactory, dataEventBus, "opord") { }

        protected override void SetCache(IEnumerable<Opord> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.start).ToList();
            }
        }
    }
}
