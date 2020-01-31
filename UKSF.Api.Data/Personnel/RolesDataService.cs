using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class RolesDataService : CachedDataService<Role, IRolesDataService>, IRolesDataService {
        public RolesDataService(IDataCollection dataCollection, IDataEventBus<IRolesDataService> dataEventBus) : base(dataCollection, dataEventBus, "roles") { }

        public override List<Role> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.name).ToList();
            return Collection;
        }

        public override Role GetSingle(string name) => GetSingle(x => x.name == name);
    }
}
