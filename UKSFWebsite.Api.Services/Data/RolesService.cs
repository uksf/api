using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Data {
    public class RolesService : CachedDataService<Role>, IRolesService {
        public RolesService(IMongoDatabase database) : base(database, "roles") {}

        public override List<Role> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.name).ToList();
            return Collection;
        }
        
        public override Role GetSingle(string name) => GetSingle(x => x.name == name);

        public int Sort(string nameA, string nameB) {
            Role roleA = GetSingle(nameA);
            Role roleB = GetSingle(nameB);
            int roleOrderA = roleA?.order ?? 0;
            int roleOrderB = roleB?.order ?? 0;
            return roleOrderA < roleOrderB ? -1 : roleOrderA > roleOrderB ? 1 : 0;
        }

        public Role GetUnitRoleByOrder(int order) => GetSingle(x => x.roleType == RoleType.UNIT && x.order == order);
    }
}
