using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IChainOfCommandService
{
    HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, DomainUnit start, DomainUnit target);
    bool InContextChainOfCommand(string id);
}

public class ChainOfCommandService(
    IUnitsContext unitsContext,
    IUnitsService unitsService,
    IRolesService rolesService,
    IHttpContextService httpContextService,
    IAccountService accountService
) : IChainOfCommandService
{
    public HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, DomainUnit start, DomainUnit target)
    {
        var chain = ResolveMode(mode, start, target).Where(x => x != recipient).ToHashSet();
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
            chain.Add(GetCommander(unitsService.GetRoot()));
            chain.CleanHashset();
        }

        // If no chain, get root unit child commanders
        if (chain.Count == 0)
        {
            foreach (var unit in unitsContext.Get(x => x.Parent == unitsService.GetRoot().Id)
                                             .Where(unit => UnitHasCommander(unit) && GetCommander(unit) != recipient))
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
        return unitsService.RolesHasMember(unit, contextDomainAccount.Id) &&
               (unit.Members.Contains(id) || unitsService.GetAllChildren(unit, true).Any(unitChild => unitChild.Members.Contains(id)));
    }

    private IEnumerable<string> ResolveMode(ChainOfCommandMode mode, DomainUnit start, DomainUnit target)
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

            unit = unitsService.GetParent(unit);
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

            var parentUnit = unitsService.GetParent(unit);
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

            unit = unitsService.GetParent(unit);
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

            unit = unitsService.GetParent(unit);
        }

        return string.Empty;
    }

    private bool UnitHasCommander(DomainUnit unit)
    {
        return unitsService.HasRole(unit, rolesService.GetCommanderRoleName());
    }

    private string GetCommander(DomainUnit unit)
    {
        if (unit == null)
        {
            return string.Empty;
        }

        return unit.Roles.GetValueOrDefault(rolesService.GetCommanderRoleName(), string.Empty);
    }
}
