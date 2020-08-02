using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Operations;

namespace UKSF.Api.Data.Operations {
    public class OperationOrderDataService : CachedDataService<Opord, IOperationOrderDataService>, IOperationOrderDataService {
        public OperationOrderDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IOperationOrderDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "opord") { }

        public override List<Opord> Collection {
            get => base.Collection;
            protected set {
                lock (LockObject) base.Collection = value?.OrderBy(x => x.start).ToList();
            }
        }
    }
}
