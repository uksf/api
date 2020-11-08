using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Command.Models;

namespace UKSF.Api.Command.Context {
    public interface IOperationReportDataService : IDataService<Oprep>, ICachedDataService { }

    public class OperationReportDataService : CachedDataService<Oprep>, IOperationReportDataService {
        public OperationReportDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Oprep> dataEventBus) : base(dataCollectionFactory, dataEventBus, "oprep") { }

        protected override void SetCache(IEnumerable<Oprep> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.start).ToList();
            }
        }
    }
}
