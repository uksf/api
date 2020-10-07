using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationReportDataService : CachedDataService<Oprep>, IOperationReportDataService {
        public OperationReportDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Oprep> dataEventBus) : base(dataCollectionFactory, dataEventBus, "oprep") { }

        protected override void SetCache(IEnumerable<Oprep> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.start).ToList();
            }
        }
    }
}
