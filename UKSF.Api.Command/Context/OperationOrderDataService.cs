using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command.Context {
    public interface IOperationOrderDataService : IDataService<Opord>, ICachedDataService { }

    public class OperationOrderDataService : CachedDataService<Opord>, IOperationOrderDataService {
        public OperationOrderDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Opord> dataEventBus) : base(dataCollectionFactory, dataEventBus, "opord") { }

        protected override void SetCache(IEnumerable<Opord> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.Start).ToList();
            }
        }
    }
}
