using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Personnel {
    public class RolesService : IRolesService {
        private readonly IRolesDataService data;

        public RolesService(IRolesDataService data) => this.data = data;

        public IRolesDataService Data() => data;

        public int Sort(string nameA, string nameB) {
            Role roleA = data.GetSingle(nameA);
            Role roleB = data.GetSingle(nameB);
            int roleOrderA = roleA?.order ?? 0;
            int roleOrderB = roleB?.order ?? 0;
            return roleOrderA < roleOrderB ? -1 : roleOrderA > roleOrderB ? 1 : 0;
        }

        public Role GetUnitRoleByOrder(int order) => data.GetSingle(x => x.roleType == RoleType.UNIT && x.order == order);
    }
}
