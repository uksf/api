using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationReportDataService : CachedDataService<Oprep, IOperationReportDataService>, IOperationReportDataService {
        public OperationReportDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IOperationReportDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "oprep") { }

        public override List<Oprep> Collection {
            get => base.Collection;
            protected set {
                lock (LockObject) base.Collection = value?.OrderBy(x => x.start).ToList();
            }
        }
    }
}
