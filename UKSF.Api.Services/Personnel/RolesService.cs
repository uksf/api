using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Personnel {
    public class RolesService : DataBackedService<IRolesDataService>, IRolesService {

        public RolesService(IRolesDataService data) : base(data) { }

        public int Sort(string nameA, string nameB) {
            Role roleA = Data.GetSingle(nameA);
            Role roleB = Data.GetSingle(nameB);
            int roleOrderA = roleA?.order ?? 0;
            int roleOrderB = roleB?.order ?? 0;
            return roleOrderA < roleOrderB ? -1 : roleOrderA > roleOrderB ? 1 : 0;
        }

        public Role GetUnitRoleByOrder(int order) => Data.GetSingle(x => x.roleType == RoleType.UNIT && x.order == order);
    }
}
