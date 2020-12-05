using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Context {
    public interface IUnitsContext : IMongoContext<Unit>, ICachedMongoContext { }

    public class UnitsContext : CachedMongoContext<Unit>, IUnitsContext {
        public UnitsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "units") { }

        protected override void SetCache(IEnumerable<Unit> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.Order).ToList();
            }
        }
    }
}
