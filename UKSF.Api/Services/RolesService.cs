using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Services;

public interface IRolesService
{
    int Sort(string nameA, string nameB);
    DomainRole GetUnitRoleByOrder(int order);
    string GetCommanderRoleName();
}

public class RolesService : IRolesService
{
    private readonly IRolesContext _rolesContext;

    public RolesService(IRolesContext rolesContext)
    {
        _rolesContext = rolesContext;
    }

    public int Sort(string nameA, string nameB)
    {
        var roleA = _rolesContext.GetSingle(nameA);
        var roleB = _rolesContext.GetSingle(nameB);
        var roleOrderA = roleA?.Order ?? 0;
        var roleOrderB = roleB?.Order ?? 0;
        return roleOrderA < roleOrderB ? -1 :
            roleOrderA > roleOrderB    ? 1 : 0;
    }

    public DomainRole GetUnitRoleByOrder(int order)
    {
        return _rolesContext.GetSingle(x => x.RoleType == RoleType.UNIT && x.Order == order);
    }

    public string GetCommanderRoleName()
    {
        return GetUnitRoleByOrder(0).Name;
    }
}
