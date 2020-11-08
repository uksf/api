using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Context {
    public interface IRolesDataService : IDataService<Role>, ICachedDataService {
        new IEnumerable<Role> Get();
        new Role GetSingle(string name);
    }

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
