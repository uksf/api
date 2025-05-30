using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IRolesService
{
    int Sort(string nameA, string nameB);
    DomainRole GetUnitRoleByOrder(int order);
    string GetCommanderRoleName();
    string GetUnitRoleNameByOrder(int order);
    int GetUnitRoleOrderByName(string roleName);
}

public class RolesService(IRolesContext rolesContext) : IRolesService
{
    private static readonly Dictionary<int, string> ChainOfCommandPositions = new()
    {
        { 0, "1iC" },
        { 1, "2iC" },
        { 2, "3iC" },
        { 3, "NCOiC" }
    };

    public int Sort(string nameA, string nameB)
    {
        var roleA = rolesContext.GetSingle(nameA);
        var roleB = rolesContext.GetSingle(nameB);
        var roleOrderA = roleA?.Order ?? 0;
        var roleOrderB = roleB?.Order ?? 0;
        return roleOrderA < roleOrderB ? -1 :
            roleOrderA > roleOrderB    ? 1 : 0;
    }

    public DomainRole GetUnitRoleByOrder(int order)
    {
        return rolesContext.GetSingle(x => x.RoleType == RoleType.Unit && x.Order == order);
    }

    public string GetCommanderRoleName()
    {
        return GetUnitRoleNameByOrder(0);
    }

    public string GetUnitRoleNameByOrder(int order)
    {
        return ChainOfCommandPositions.GetValueOrDefault(order, string.Empty);
    }

    public int GetUnitRoleOrderByName(string roleName)
    {
        return ChainOfCommandPositions.FirstOrDefault(x => x.Value == roleName).Key;
    }
}
