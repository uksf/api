using System;
using System.Collections.Generic;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationReportDataService : CachedDataService<Oprep, IOperationReportDataService>, IOperationReportDataService {
        public OperationReportDataService(IDataCollection dataCollection, IDataEventBus<IOperationReportDataService> dataEventBus) : base(dataCollection, dataEventBus, "oprep") { }

        public override List<Oprep> Get() {
            List<Oprep> reversed = new List<Oprep>(base.Get());
            reversed.Reverse();
            return reversed;
        }

        public override List<Oprep> Get(Func<Oprep, bool> predicate) {
            List<Oprep> reversed = new List<Oprep>(base.Get(predicate));
            reversed.Reverse();
            return reversed;
        }
    }
}
