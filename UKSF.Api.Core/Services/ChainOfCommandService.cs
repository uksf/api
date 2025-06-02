using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IChainOfCommandService
{
    HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, DomainUnit start, DomainUnit target);
    bool InContextChainOfCommand(string id);
    string GetCommanderPositionName();
    string GetChainOfCommandPositionName(int order);
    int GetChainOfCommandPositionOrder(string positionName);
    Task SetMemberChainOfCommandPosition(string memberId, string unitId, string position = "");
    Task SetMemberChainOfCommandPosition(string memberId, DomainUnit unit, string position = "");
    bool HasChainOfCommandPosition(string unitId, string position);
    bool HasChainOfCommandPosition(DomainUnit unit, string position);
    bool ChainOfCommandHasMember(string unitId, string id);
    bool ChainOfCommandHasMember(DomainUnit unit, string id);
    bool MemberHasChainOfCommandPosition(string id, string unitId, string position);
    bool MemberHasChainOfCommandPosition(string id, DomainUnit unit, string position);
    bool MemberHasAnyChainOfCommandPosition(string id);
    bool MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(string id);
    int GetMemberChainOfCommandOrder(DomainAccount account, DomainUnit unit);
    string GetChainOfCommandPosition(DomainUnit unit, string accountId);
}

public class ChainOfCommandService(IUnitsContext unitsContext, IHttpContextService httpContextService, IAccountService accountService) : IChainOfCommandService
{
    private static readonly Dictionary<int, string> ChainOfCommandPositions = new()
    {
        { 0, "1iC" },
        { 1, "2iC" },
        { 2, "3iC" },
        { 3, "NCOiC" }
    };

    public HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, DomainUnit start, DomainUnit target)
    {
        var chain = ResolveMode(mode, recipient, start, target).Where(x => x != recipient).ToHashSet();
        chain.CleanHashset();

        // If no chain, and mode is not next commander, get next commander
        if (chain.Count == 0 && mode != ChainOfCommandMode.Next_Commander && mode != ChainOfCommandMode.Next_Commander_Exclude_Self)
        {
            chain = GetNextCommander(start).Where(x => x != recipient).ToHashSet();
            chain.CleanHashset();
        }

        // If no chain, get root unit commander
        if (chain.Count == 0)
        {
            chain.Add(GetCommander(GetCombatRoot()));
            chain.CleanHashset();
        }

        // If no chain, get root unit child commanders
        if (chain.Count == 0)
        {
            foreach (var unit in unitsContext.Get(x => x.Parent == GetCombatRoot().Id).Where(unit => UnitHasCommander(unit) && GetCommander(unit) != recipient))
            {
                chain.Add(GetCommander(unit));
            }

            chain.CleanHashset();
        }

        // If no chain, get personnel
        if (chain.Count == 0)
        {
            chain.UnionWith(GetPersonnel());
            chain.CleanHashset();
        }

        return chain;
    }

    public bool InContextChainOfCommand(string id)
    {
        var contextDomainAccount = accountService.GetUserAccount();
        if (id == contextDomainAccount.Id)
        {
            return true;
        }

        var unit = unitsContext.GetSingle(x => x.Name == contextDomainAccount.UnitAssignment);
        return ChainOfCommandHasMember(unit, contextDomainAccount.Id) &&
               (unit.Members.Contains(id) || GetAllChildren(unit, true).Any(unitChild => unitChild.Members.Contains(id)));
    }

    public string GetCommanderPositionName()
    {
        return GetChainOfCommandPositionName(0);
    }

    public string GetChainOfCommandPositionName(int order)
    {
        return ChainOfCommandPositions.GetValueOrDefault(order, string.Empty);
    }

    public int GetChainOfCommandPositionOrder(string positionName)
    {
        var position = ChainOfCommandPositions.FirstOrDefault(x => x.Value == positionName);
        return position.Key;
    }

    private IEnumerable<string> ResolveMode(ChainOfCommandMode mode, string recipient, DomainUnit start, DomainUnit target)
    {
        return mode switch
        {
            ChainOfCommandMode.Full                           => Full(start),
            ChainOfCommandMode.Next_Commander                 => GetNextCommander(start),
            ChainOfCommandMode.Next_Commander_Exclude_Self    => GetNextCommanderExcludeSelf(start),
            ChainOfCommandMode.Commander_And_One_Above        => CommanderAndOneAbove(start),
            ChainOfCommandMode.Commander_And_Personnel        => GetCommanderAndPersonnel(start),
            ChainOfCommandMode.Commander_And_Target_Commander => GetCommanderAndTargetCommander(start, target),
            ChainOfCommandMode.Personnel                      => GetPersonnel(),
            ChainOfCommandMode.Target_Commander               => GetNextCommander(target),
            _                                                 => throw new InvalidOperationException("Chain of command mode not recognized")
        };
    }

    private IEnumerable<string> Full(DomainUnit unit)
    {
        HashSet<string> chain = new();
        while (unit is not null)
        {
            if (UnitHasCommander(unit))
            {
                chain.Add(GetCommander(unit));
            }

            unit = GetParent(unit);
        }

        return chain;
    }

    private IEnumerable<string> GetNextCommander(DomainUnit unit)
    {
        return new HashSet<string> { GetNextUnitCommander(unit) };
    }

    private IEnumerable<string> GetNextCommanderExcludeSelf(DomainUnit unit)
    {
        return new HashSet<string> { GetNextUnitCommanderExcludeSelf(unit) };
    }

    private IEnumerable<string> CommanderAndOneAbove(DomainUnit unit)
    {
        HashSet<string> chain = new();
        if (unit is not null)
        {
            if (UnitHasCommander(unit))
            {
                chain.Add(GetCommander(unit));
            }

            var parentUnit = GetParent(unit);
            if (parentUnit is not null && UnitHasCommander(parentUnit))
            {
                chain.Add(GetCommander(parentUnit));
            }
        }

        return chain;
    }

    private IEnumerable<string> GetCommanderAndPersonnel(DomainUnit unit)
    {
        HashSet<string> chain = new();
        if (UnitHasCommander(unit))
        {
            chain.Add(GetCommander(unit));
        }

        chain.UnionWith(GetPersonnel());
        return chain;
    }

    private IEnumerable<string> GetPersonnel()
    {
        var unit = unitsContext.GetSingle(x => x.Shortname == "SR7");
        return unit?.Members.ToHashSet() ?? new HashSet<string>();
    }

    private IEnumerable<string> GetCommanderAndTargetCommander(DomainUnit unit, DomainUnit targetUnit)
    {
        return new HashSet<string> { GetNextUnitCommander(unit), GetNextUnitCommander(targetUnit) };
    }

    private string GetNextUnitCommander(DomainUnit unit)
    {
        while (unit is not null)
        {
            if (UnitHasCommander(unit))
            {
                return GetCommander(unit);
            }

            unit = GetParent(unit);
        }

        return string.Empty;
    }

    private string GetNextUnitCommanderExcludeSelf(DomainUnit unit)
    {
        while (unit is not null)
        {
            if (UnitHasCommander(unit))
            {
                var commander = GetCommander(unit);
                if (commander != httpContextService.GetUserId())
                {
                    return commander;
                }
            }

            unit = GetParent(unit);
        }

        return string.Empty;
    }

    private bool UnitHasCommander(DomainUnit unit)
    {
        return HasChainOfCommandPosition(unit, "1iC");
    }

    private string GetCommander(DomainUnit unit)
    {
        if (unit == null)
        {
            return string.Empty;
        }

        return unit.ChainOfCommand?.First ?? string.Empty;
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

    public bool HasChainOfCommandPosition(string unitId, string position)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return HasChainOfCommandPosition(unit, position);
    }

    public bool HasChainOfCommandPosition(DomainUnit unit, string position)
    {
        return unit?.ChainOfCommand?.HasPosition(position) ?? false;
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

    public bool MemberHasChainOfCommandPosition(string id, string unitId, string position)
    {
        var unit = unitsContext.GetSingle(x => x.Id == unitId);
        return MemberHasChainOfCommandPosition(id, unit, position);
    }

    public bool MemberHasChainOfCommandPosition(string id, DomainUnit unit, string position)
    {
        return unit?.ChainOfCommand?.GetMemberAtPosition(position) == id;
    }

    public bool MemberHasAnyChainOfCommandPosition(string id)
    {
        return unitsContext.Get().Any(x => ChainOfCommandHasMember(x, id));
    }

    public bool MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(string id)
    {
        return unitsContext.Get(x => x.Branch == UnitBranch.Combat || x.Branch == UnitBranch.Auxiliary).Any(x => ChainOfCommandHasMember(x, id));
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
                var order = GetChainOfCommandPositionOrder(memberPosition.Position);
                return int.MaxValue - order;
            }
        }

        return -1;
    }

    public string GetChainOfCommandPosition(DomainUnit unit, string accountId)
    {
        var assignedPositions = unit.ChainOfCommand?.GetAssignedPositions();
        var memberPosition = assignedPositions?.FirstOrDefault(x => x.MemberId == accountId);
        return memberPosition?.Position ?? "";
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

    private DomainUnit GetCombatRoot()
    {
        return unitsContext.GetSingle(x => x.Parent == ObjectId.Empty.ToString() && x.Branch == UnitBranch.Combat);
    }

    private DomainUnit GetParent(DomainUnit unit)
    {
        return unit != null && unit.Parent != string.Empty ? unitsContext.GetSingle(x => x.Id == unit.Parent) : null;
    }

    private IEnumerable<DomainUnit> GetAllChildren(DomainUnit parent, bool includeParent = false)
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
}
