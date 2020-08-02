using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class RolesDataService : CachedDataService<Role, IRolesDataService>, IRolesDataService {
        public RolesDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IRolesDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "roles") { }

        public override List<Role> Collection {
            get => base.Collection;
            protected set {
                lock (LockObject) base.Collection = value?.OrderBy(x => x.name).ToList();
            }
        }

        public override Role GetSingle(string name) => GetSingle(x => x.name == name);
    }
}
