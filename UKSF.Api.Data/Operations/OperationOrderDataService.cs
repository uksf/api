using System;
using System.Collections.Generic;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationOrderDataService : CachedDataService<Opord, IOperationOrderDataService>, IOperationOrderDataService {
        public OperationOrderDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IOperationOrderDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "opord") { }

        public override List<Opord> Get() {
            List<Opord> reversed = new List<Opord>(base.Get());
            reversed.Reverse();
            return reversed;
        }

        public override List<Opord> Get(Func<Opord, bool> predicate) {
            List<Opord> reversed = new List<Opord>(base.Get(predicate));
            reversed.Reverse();
            return reversed;
        }
    }
}
