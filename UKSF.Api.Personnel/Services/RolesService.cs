using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services {
    public interface IRolesService {
        int Sort(string nameA, string nameB);
        Role GetUnitRoleByOrder(int order);
        string GetCommanderRoleName();
    }

    public class RolesService : IRolesService {
        private readonly IRolesContext _rolesContext;

        public RolesService(IRolesContext rolesContext) => _rolesContext = rolesContext;

        public int Sort(string nameA, string nameB) {
            Role roleA = _rolesContext.GetSingle(nameA);
            Role roleB = _rolesContext.GetSingle(nameB);
            int roleOrderA = roleA?.Order ?? 0;
            int roleOrderB = roleB?.Order ?? 0;
            return roleOrderA < roleOrderB ? -1 : roleOrderA > roleOrderB ? 1 : 0;
        }

        public Role GetUnitRoleByOrder(int order) => _rolesContext.GetSingle(x => x.RoleType == RoleType.UNIT && x.Order == order);

        public string GetCommanderRoleName() => GetUnitRoleByOrder(0).Name;
    }
}
