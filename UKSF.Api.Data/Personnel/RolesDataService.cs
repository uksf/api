using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class RolesDataService : CachedDataService<Role, IRolesDataService>, IRolesDataService {
        public RolesDataService(IMongoDatabase database, IDataEventBus<IRolesDataService> dataEventBus) : base(database, dataEventBus, "roles") { }

        public override List<Role> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.name).ToList();
            return Collection;
        }

        public override Role GetSingle(string name) => GetSingle(x => x.name == name);
    }
}
