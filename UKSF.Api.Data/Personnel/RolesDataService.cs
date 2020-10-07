using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class RolesDataService : CachedDataService<Role>, IRolesDataService {
        public RolesDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Role> dataEventBus) : base(dataCollectionFactory, dataEventBus, "roles") { }

        protected override void SetCache(IEnumerable<Role> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.name).ToList();
            }
        }

        public override Role GetSingle(string name) => GetSingle(x => x.name == name);
    }
}
