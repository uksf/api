using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IUnitsService
{
    UnitDto GetSingle(string id);
    IEnumerable<DomainUnit> GetSortedUnits(Func<DomainUnit, bool> predicate = null);
    Task AddMember(string id, string unitId);
    Task RemoveMember(string id, string unitName);
    Task RemoveMember(string id, DomainUnit unit);
    Task SetMemberRole(string roleId, string unitId, string role = "");
    Task SetMemberRole(string roleId, DomainUnit unit, string role = "");
    Task RenameRole(string oldName, string newName);
    Task DeleteRole(string role);
    bool HasRole(string unitId, string role);
    bool HasRole(DomainUnit unit, string role);
    bool HasMember(string unitId, string memberId);
    bool AnyChildHasMember(string unitId, string memberId);
    bool AnyParentHasMember(string unitId, string memberId);
    bool RolesHasMember(string unitId, string id);
    bool RolesHasMember(DomainUnit unit, string id);
    bool MemberHasRole(string id, string unitId, string role);
    bool MemberHasRole(string id, DomainUnit unit, string role);
    bool MemberHasAnyRole(string id);
    int GetMemberRoleOrder(DomainAccount account, DomainUnit unit);
    DomainUnit GetRoot();
    DomainUnit GetAuxiliaryRoot();
    DomainUnit GetSecondaryRoot();
    DomainUnit GetParent(DomainUnit unit);
    IEnumerable<DomainUnit> GetParents(DomainUnit unit);
    IEnumerable<DomainUnit> GetChildren(DomainUnit parent);
    IEnumerable<DomainUnit> GetAllChildren(DomainUnit parent, bool includeParent = false);
    int GetUnitDepth(DomainUnit unit);
    string GetChainString(DomainUnit unit);
    IEnumerable<UnitMemberDto> MapUnitMembers(DomainUnit unit);
}

public class UnitsService(
    IUnitsContext unitsContext,
    IRolesContext rolesContext,
    IRanksService ranksService,
    IRolesService rolesService,
    IDisplayNameService displayNameService,
    IAccountContext accountContext,
    IUnitMapper unitMapper
) : IUnitsService
{
    public UnitDto GetSingle(string id)
    {
        var unit = unitsContext.GetSingle(id);
        var parent = GetParent(unit);
        return unitMapper.Map(unit, GetChainString(unit), parent?.Name, MapUnitMembers(unit));
    }

    public IEnumerable<DomainUnit> GetSortedUnits(Func<DomainUnit, bool> predicate = null)
    {
        List<DomainUnit> sortedUnits = [];
        var combatRoot = unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.Combat);
        var auxiliaryRoot = unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.Auxiliary);
        var secondaryRoot = unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.Secondary);

        sortedUnits.Add(combatRoot);
        sortedUnits.AddRange(GetAllChildren(combatRoot));
        sortedUnits.Add(auxiliaryRoot);
        sortedUnits.AddRange(GetAllChildren(auxiliaryRoot));
        sortedUnits.Add(secondaryRoot);
        sortedUnits.AddRange(GetAllChildren(secondaryRoot));

        return predicate is not null ? sortedUnits.Where(predicate) : sortedUnits;
    }

    public async Task AddMember(string id, string unitId)
    {
        if (unitsContext.GetSingle(x => x.Id == unitId && x.Members.Contains(id)) is not null)
        {
            return;
        }

        await unitsContext.Update(unitId, Builders<DomainUnit>.Update.Push(x => x.Members, id));
    }

    public async Task RemoveMember(string id, string unitName)
    {
        var unit = unitsContext.GetSingle(x => x.Name == unitName);
        if (unit == null)
        {
            return;
        }

        await RemoveMember(id, unit);
    }

    public async Task RemoveMember(string id, DomainUnit unit)
    {
        if (unit.Members.Contains(id))
        {
            await unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Pull(x => x.Members, id));
        }

        await RemoveMemberRoles(id, unit);
    }

    public async Task SetMemberRole(string roleId, string unitId, string role = "")
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        if (unit == null)
        {
            return;
        }

        await SetMemberRole(roleId, unit, role);
    }

    public async Task SetMemberRole(string roleId, DomainUnit unit, string role = "")
    {
        await RemoveMemberRoles(roleId, unit);
        if (!string.IsNullOrEmpty(role))
        {
            await unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Set($"roles.{role}", roleId));
        }
    }

    public async Task RenameRole(string oldName, string newName)
    {
        foreach (var unit in unitsContext.Get(x => x.Roles.ContainsKey(oldName)))
        {
            var id = unit.Roles[oldName];
            await unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Unset($"roles.{oldName}"));
            await unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Set($"roles.{newName}", id));
        }
    }

    public async Task DeleteRole(string role)
    {
        foreach (var unit in from unit in unitsContext.Get(x => x.Roles.ContainsKey(role)) let id = unit.Roles[role] select unit)
        {
            await unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Unset($"roles.{role}"));
        }
    }

    public bool HasRole(string unitId, string role)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return HasRole(unit, role);
    }

    public bool HasRole(DomainUnit unit, string role)
    {
        return unit.Roles.ContainsKey(role);
    }

    public bool HasMember(string unitId, string memberId)
    {
        var unit = unitsContext.GetSingle(unitId);
        return unit.Members.Contains(memberId);
    }

    public bool AnyChildHasMember(string unitId, string memberId)
    {
        var unit = unitsContext.GetSingle(unitId);
        var units = GetAllChildren(unit, true);
        return units.Any(x => x.Members.Contains(memberId));
    }

    public bool AnyParentHasMember(string unitId, string memberId)
    {
        var unit = unitsContext.GetSingle(unitId);
        var units = GetParents(unit);
        return units.Any(x => x.Members.Contains(memberId));
    }

    public bool RolesHasMember(string unitId, string id)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return RolesHasMember(unit, id);
    }

    public bool RolesHasMember(DomainUnit unit, string id)
    {
        return unit.Roles.ContainsValue(id);
    }

    public bool MemberHasRole(string id, string unitId, string role)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return MemberHasRole(id, unit, role);
    }

    public bool MemberHasRole(string id, DomainUnit unit, string role)
    {
        return unit.Roles.GetValueOrDefault(role, string.Empty) == id;
    }

    public bool MemberHasAnyRole(string id)
    {
        return unitsContext.Get().Any(x => RolesHasMember(x, id));
    }

    public int GetMemberRoleOrder(DomainAccount account, DomainUnit unit)
    {
        if (RolesHasMember(unit, account.Id))
        {
            var role = rolesContext.GetSingle(x =>
                {
                    var accountRole = unit.Roles.FirstOrDefault(y => y.Value == account.Id);
                    return x.Name == accountRole.Key;
                }
            );
            return int.MaxValue - role.Order;
        }

        return -1;
    }

    public DomainUnit GetRoot()
    {
        return unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.Combat);
    }

    public DomainUnit GetAuxiliaryRoot()
    {
        return unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.Auxiliary);
    }

    public DomainUnit GetSecondaryRoot()
    {
        return unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.Secondary);
    }

    public DomainUnit GetParent(DomainUnit unit)
    {
        return unit.Parent != string.Empty ? unitsContext.GetSingle(x => x.Id == unit.Parent) : null;
    }

    // TODO: Change this to not add the child unit to the return
    public IEnumerable<DomainUnit> GetParents(DomainUnit unit)
    {
        if (unit == null)
        {
            return new List<DomainUnit>();
        }

        List<DomainUnit> parentUnits = [];
        do
        {
            parentUnits.Add(unit);
            var child = unit;
            unit = !string.IsNullOrEmpty(unit.Parent) ? unitsContext.GetSingle(x => x.Id == child.Parent) : null;
            if (unit == child)
            {
                break;
            }
        }
        while (unit is not null);

        return parentUnits;
    }

    public IEnumerable<DomainUnit> GetChildren(DomainUnit parent)
    {
        return unitsContext.Get(x => x.Parent == parent.Id).ToList();
    }

    public IEnumerable<DomainUnit> GetAllChildren(DomainUnit parent, bool includeParent = false)
    {
        var children = includeParent ? [parent] : new List<DomainUnit>();
        foreach (var unit in unitsContext.Get(x => x.Parent == parent.Id))
        {
            children.Add(unit);
            children.AddRange(GetAllChildren(unit));
        }

        return children;
    }

    public int GetUnitDepth(DomainUnit unit)
    {
        if (unit.Parent == ObjectId.Empty.ToString())
        {
            return 0;
        }

        var depth = 0;
        var parent = unitsContext.GetSingle(unit.Parent);
        while (parent is not null)
        {
            depth++;
            parent = unitsContext.GetSingle(parent.Parent);
        }

        return depth;
    }

    public string GetChainString(DomainUnit unit)
    {
        var parentUnits = GetParents(unit).Skip(1).ToList();
        var unitNames = unit.Name;
        parentUnits.ForEach(x => unitNames += $", {x.Name}");
        return unitNames;
    }

    private async Task RemoveMemberRoles(string roleId, DomainUnit unit)
    {
        var roles = unit.Roles;
        var originalCount = unit.Roles.Count;
        foreach (var (key, _) in roles.Where(x => x.Value == roleId).ToList())
        {
            roles.Remove(key);
        }

        if (roles.Count != originalCount)
        {
            await unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Set(x => x.Roles, roles));
        }
    }

    public IEnumerable<UnitMemberDto> MapUnitMembers(DomainUnit unit)
    {
        return SortMembers(unit.Members, unit).Select(x => MapUnitMember(x, unit));
    }

    private UnitMemberDto MapUnitMember(DomainAccount member, DomainUnit unit)
    {
        return new UnitMemberDto
        {
            Name = displayNameService.GetDisplayName(member),
            Role = member.RoleAssignment,
            UnitRole = GetRole(unit, member.Id)
        };
    }

    private IEnumerable<DomainAccount> SortMembers(IEnumerable<string> members, DomainUnit unit)
    {
        return members.Select(x =>
                          {
                              var account = accountContext.GetSingle(x);
                              return new
                              {
                                  account,
                                  rankIndex = ranksService.GetRankOrder(account.Rank),
                                  roleIndex = GetMemberRoleOrder(account, unit)
                              };
                          }
                      )
                      .OrderByDescending(x => x.roleIndex)
                      .ThenBy(x => x.rankIndex)
                      .ThenBy(x => x.account.Lastname)
                      .ThenBy(x => x.account.Firstname)
                      .Select(x => x.account);
    }

    private string GetRole(DomainUnit unit, string accountId)
    {
        return MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(0).Name) ? "1" :
            MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(1).Name)    ? "2" :
            MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(2).Name)    ? "3" :
            MemberHasRole(accountId, unit, rolesService.GetUnitRoleByOrder(3).Name)    ? "N" : "";
    }
}
