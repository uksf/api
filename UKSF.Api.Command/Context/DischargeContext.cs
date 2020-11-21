using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command.Context {
    public interface IDischargeContext : IMongoContext<DischargeCollection>, ICachedMongoContext { }

    public class DischargeContext : CachedMongoContext<DischargeCollection>, IDischargeContext {
        public DischargeContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<DischargeCollection> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "discharges") { }

        protected override void SetCache(IEnumerable<DischargeCollection> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderByDescending(x => x.Discharges.Last().Timestamp).ToList();
            }
        }
    }
}
