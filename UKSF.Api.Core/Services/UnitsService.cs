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
    Task SetMemberChainOfCommandPosition(string memberId, string unitId, string position = "");
    Task SetMemberChainOfCommandPosition(string memberId, DomainUnit unit, string position = "");
    Task RenameRole(string oldName, string newName);
    Task DeleteRole(string role);
    bool HasRole(string unitId, string role);
    bool HasRole(DomainUnit unit, string role);
    bool HasChainOfCommandPosition(string unitId, string position);
    bool HasChainOfCommandPosition(DomainUnit unit, string position);
    bool HasMember(string unitId, string memberId);
    bool AnyChildHasMember(string unitId, string memberId);
    bool AnyParentHasMember(string unitId, string memberId);
    bool RolesHasMember(string unitId, string id);
    bool RolesHasMember(DomainUnit unit, string id);
    bool ChainOfCommandHasMember(string unitId, string id);
    bool ChainOfCommandHasMember(DomainUnit unit, string id);
    bool MemberHasRole(string id, string unitId, string role);
    bool MemberHasRole(string id, DomainUnit unit, string role);
    bool MemberHasChainOfCommandPosition(string id, string unitId, string position);
    bool MemberHasChainOfCommandPosition(string id, DomainUnit unit, string position);
    bool MemberHasAnyRole(string id);
    bool MemberHasAnyChainOfCommandPosition(string id);
    int GetMemberRoleOrder(DomainAccount account, DomainUnit unit);
    int GetMemberChainOfCommandOrder(DomainAccount account, DomainUnit unit);
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
        await RemoveMemberFromChainOfCommand(id, unit);
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

    public async Task SetMemberChainOfCommandPosition(string memberId, string unitId, string position = "")
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        if (unit == null)
        {
            return;
        }

        await SetMemberChainOfCommandPosition(memberId, unit, position);
    }

    public async Task SetMemberChainOfCommandPosition(string memberId, DomainUnit unit, string position = "")
    {
        // Remove member from all current positions
        await RemoveMemberFromChainOfCommand(memberId, unit);

        // Set new position if provided
        if (!string.IsNullOrEmpty(position))
        {
            var updatedChainOfCommand = unit.ChainOfCommand ?? new ChainOfCommand();
            updatedChainOfCommand.SetMemberAtPosition(position, memberId);
            await unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Set(x => x.ChainOfCommand, updatedChainOfCommand));
        }
    }

    public async Task RenameRole(string oldName, string newName)
    {
        // Legacy method - unit roles are now handled by chain of command
        // This method is kept for backwards compatibility but does nothing
        await Task.CompletedTask;
    }

    public async Task DeleteRole(string role)
    {
        // Legacy method - unit roles are now handled by chain of command
        // This method is kept for backwards compatibility but does nothing
        await Task.CompletedTask;
    }

    public bool HasRole(string unitId, string role)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return HasRole(unit, role);
    }

    public bool HasRole(DomainUnit unit, string role)
    {
        return unit?.Roles.ContainsKey(role) ?? false;
    }

    public bool HasChainOfCommandPosition(string unitId, string position)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return HasChainOfCommandPosition(unit, position);
    }

    public bool HasChainOfCommandPosition(DomainUnit unit, string position)
    {
        return unit?.ChainOfCommand?.HasPosition(position) ?? false;
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
        return unit?.Roles.ContainsValue(id) ?? false;
    }

    public bool ChainOfCommandHasMember(string unitId, string id)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return ChainOfCommandHasMember(unit, id);
    }

    public bool ChainOfCommandHasMember(DomainUnit unit, string id)
    {
        return unit?.ChainOfCommand?.HasMember(id) ?? false;
    }

    public bool MemberHasRole(string id, string unitId, string role)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return MemberHasRole(id, unit, role);
    }

    public bool MemberHasRole(string id, DomainUnit unit, string role)
    {
        return unit?.Roles.GetValueOrDefault(role, string.Empty) == id;
    }

    public bool MemberHasChainOfCommandPosition(string id, string unitId, string position)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return MemberHasChainOfCommandPosition(id, unit, position);
    }

    public bool MemberHasChainOfCommandPosition(string id, DomainUnit unit, string position)
    {
        return unit?.ChainOfCommand?.GetMemberAtPosition(position) == id;
    }

    public bool MemberHasAnyRole(string id)
    {
        return unitsContext.Get().Any(x => RolesHasMember(x, id));
    }

    public bool MemberHasAnyChainOfCommandPosition(string id)
    {
        return unitsContext.Get().Any(x => ChainOfCommandHasMember(x, id));
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

    public int GetMemberChainOfCommandOrder(DomainAccount account, DomainUnit unit)
    {
        if (ChainOfCommandHasMember(unit, account.Id))
        {
            // Find which position the member holds
            var assignedPositions = unit.ChainOfCommand.GetAssignedPositions();
            var memberPosition = assignedPositions.FirstOrDefault(x => x.MemberId == account.Id);
            if (!string.IsNullOrEmpty(memberPosition.Position))
            {
                var order = rolesService.GetUnitRoleOrderByName(memberPosition.Position);
                return int.MaxValue - order;
            }
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
        return unit != null && unit.Parent != string.Empty ? unitsContext.GetSingle(x => x.Id == unit.Parent) : null;
    }

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
        return parent != null ? unitsContext.Get(x => x.Parent == parent.Id).ToList() : [];
    }

    public IEnumerable<DomainUnit> GetAllChildren(DomainUnit parent, bool includeParent = false)
    {
        if (parent == null)
        {
            return new List<DomainUnit>();
        }

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
        if (unit?.Parent == ObjectId.Empty.ToString())
        {
            return 0;
        }

        var depth = 0;
        var parent = unit != null ? unitsContext.GetSingle(unit.Parent) : null;
        while (parent is not null)
        {
            depth++;
            parent = unitsContext.GetSingle(parent.Parent);
        }

        return depth;
    }

    public string GetChainString(DomainUnit unit)
    {
        if (unit == null)
        {
            return string.Empty;
        }

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

    private async Task RemoveMemberFromChainOfCommand(string memberId, DomainUnit unit)
    {
        if (unit.ChainOfCommand?.HasMember(memberId) == true)
        {
            var updatedChainOfCommand = unit.ChainOfCommand;
            updatedChainOfCommand.RemoveMember(memberId);
            await unitsContext.Update(unit.Id, Builders<DomainUnit>.Update.Set(x => x.ChainOfCommand, updatedChainOfCommand));
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
            UnitRole = GetChainOfCommandRole(unit, member.Id)
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
                                  roleIndex = GetMemberChainOfCommandOrder(account, unit)
                              };
                          }
                      )
                      .OrderByDescending(x => x.roleIndex)
                      .ThenBy(x => x.rankIndex)
                      .ThenBy(x => x.account.Lastname)
                      .ThenBy(x => x.account.Firstname)
                      .Select(x => x.account);
    }

    private string GetChainOfCommandRole(DomainUnit unit, string accountId)
    {
        if (MemberHasChainOfCommandPosition(accountId, unit, "1iC")) return "1";
        if (MemberHasChainOfCommandPosition(accountId, unit, "2iC")) return "2";
        if (MemberHasChainOfCommandPosition(accountId, unit, "3iC")) return "3";
        if (MemberHasChainOfCommandPosition(accountId, unit, "NCOiC")) return "N";
        return "";
    }
}
