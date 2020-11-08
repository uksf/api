using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Models;

namespace UKSF.Api.Command.Context {
    public interface IDischargeDataService : IDataService<DischargeCollection>, ICachedDataService { }

    public class DischargeDataService : CachedDataService<DischargeCollection>, IDischargeDataService {
        public DischargeDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<DischargeCollection> dataEventBus) : base(dataCollectionFactory, dataEventBus, "discharges") { }

        protected override void SetCache(IEnumerable<DischargeCollection> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderByDescending(x => x.discharges.Last().timestamp).ToList();
            }
        }
    }
}
