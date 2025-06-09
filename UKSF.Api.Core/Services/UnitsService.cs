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
    bool HasMember(string unitId, string memberId);
    bool AnyChildHasMember(string unitId, string memberId);
    bool AnyParentHasMember(string unitId, string memberId);
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
    IRanksService ranksService,
    IChainOfCommandService chainOfCommandService,
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

        // Use ChainOfCommandService to remove from chain of command
        await chainOfCommandService.SetMemberChainOfCommandPosition(id, unit);
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
            ChainOfCommandPosition = chainOfCommandService.GetChainOfCommandPosition(unit, member.Id)
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
                                  roleIndex = chainOfCommandService.GetMemberChainOfCommandOrder(account, unit)
                              };
                          }
                      )
                      .OrderByDescending(x => x.roleIndex)
                      .ThenBy(x => x.rankIndex)
                      .ThenBy(x => x.account.Lastname)
                      .ThenBy(x => x.account.Firstname)
                      .Select(x => x.account);
    }
}
