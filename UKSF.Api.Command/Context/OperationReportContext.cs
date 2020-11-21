using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command.Context {
    public interface IOperationReportContext : IMongoContext<Oprep>, ICachedMongoContext { }

    public class OperationReportContext : CachedMongoContext<Oprep>, IOperationReportContext {
        public OperationReportContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<Oprep> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "oprep") { }

        protected override void SetCache(IEnumerable<Oprep> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.Start).ToList();
            }
        }
    }
}
