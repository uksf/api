using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services {
    public interface IRolesService : IDataBackedService<IRolesDataService> {
        int Sort(string nameA, string nameB);
        Role GetUnitRoleByOrder(int order);
    }

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
