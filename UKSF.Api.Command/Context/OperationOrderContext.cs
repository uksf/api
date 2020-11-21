using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command.Context {
    public interface IOperationOrderContext : IMongoContext<Opord>, ICachedMongoContext { }

    public class OperationOrderContext : CachedMongoContext<Opord>, IOperationOrderContext {
        public OperationOrderContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<Opord> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "opord") { }

        protected override void SetCache(IEnumerable<Opord> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.Start).ToList();
            }
        }
    }
}
