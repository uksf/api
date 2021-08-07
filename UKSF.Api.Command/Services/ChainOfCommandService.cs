using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Command.Services
{
    public interface IChainOfCommandService
    {
        HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, DomainUnit start, DomainUnit target);
        bool InContextChainOfCommand(string id);
    }

    public class ChainOfCommandService : IChainOfCommandService
    {
        private readonly IAccountService _accountService;
        private readonly IHttpContextService _httpContextService;
        private readonly IRolesService _rolesService;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;

        public ChainOfCommandService(
            IUnitsContext unitsContext,
            IUnitsService unitsService,
            IRolesService rolesService,
            IHttpContextService httpContextService,
            IAccountService accountService
        )
        {
            _unitsContext = unitsContext;
            _unitsService = unitsService;
            _rolesService = rolesService;
            _httpContextService = httpContextService;
            _accountService = accountService;
        }

        public HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, DomainUnit start, DomainUnit target)
        {
            HashSet<string> chain = ResolveMode(mode, start, target).Where(x => x != recipient).ToHashSet();
            chain.CleanHashset();

            // If no chain, and mode is not next commander, get next commander
            if (chain.Count == 0 && mode != ChainOfCommandMode.NEXT_COMMANDER && mode != ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF)
            {
                chain = GetNextCommander(start).Where(x => x != recipient).ToHashSet();
                chain.CleanHashset();
            }

            // If no chain, get root unit commander
            if (chain.Count == 0)
            {
                chain.Add(GetCommander(_unitsService.GetRoot()));
                chain.CleanHashset();
            }

            // If no chain, get root unit child commanders
            if (chain.Count == 0)
            {
                foreach (var unit in _unitsContext.Get(x => x.Parent == _unitsService.GetRoot().Id)
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
            DomainAccount contextDomainAccount = _accountService.GetUserAccount();
            if (id == contextDomainAccount.Id)
            {
                return true;
            }

            var unit = _unitsContext.GetSingle(x => x.Name == contextDomainAccount.UnitAssignment);
            return _unitsService.RolesHasMember(unit, contextDomainAccount.Id) &&
                   (unit.Members.Contains(id) || _unitsService.GetAllChildren(unit, true).Any(unitChild => unitChild.Members.Contains(id)));
        }

        private IEnumerable<string> ResolveMode(ChainOfCommandMode mode, DomainUnit start, DomainUnit target)
        {
            return mode switch
            {
                ChainOfCommandMode.FULL                           => Full(start),
                ChainOfCommandMode.NEXT_COMMANDER                 => GetNextCommander(start),
                ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF    => GetNextCommanderExcludeSelf(start),
                ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE        => CommanderAndOneAbove(start),
                ChainOfCommandMode.COMMANDER_AND_PERSONNEL        => GetCommanderAndPersonnel(start),
                ChainOfCommandMode.COMMANDER_AND_TARGET_COMMANDER => GetCommanderAndTargetCommander(start, target),
                ChainOfCommandMode.PERSONNEL                      => GetPersonnel(),
                ChainOfCommandMode.TARGET_COMMANDER               => GetNextCommander(target),
                _                                                 => throw new InvalidOperationException("Chain of command mode not recognized")
            };
        }

        private IEnumerable<string> Full(DomainUnit unit)
        {
            HashSet<string> chain = new();
            while (unit != null)
            {
                if (UnitHasCommander(unit))
                {
                    chain.Add(GetCommander(unit));
                }

                unit = _unitsService.GetParent(unit);
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
            if (unit != null)
            {
                if (UnitHasCommander(unit))
                {
                    chain.Add(GetCommander(unit));
                }

                var parentUnit = _unitsService.GetParent(unit);
                if (parentUnit != null && UnitHasCommander(parentUnit))
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
            return _unitsContext.GetSingle(x => x.Shortname == "SR7").Members.ToHashSet();
        }

        private IEnumerable<string> GetCommanderAndTargetCommander(DomainUnit unit, DomainUnit targetUnit)
        {
            return new HashSet<string> { GetNextUnitCommander(unit), GetNextUnitCommander(targetUnit) };
        }

        private string GetNextUnitCommander(DomainUnit unit)
        {
            while (unit != null)
            {
                if (UnitHasCommander(unit))
                {
                    return GetCommander(unit);
                }

                unit = _unitsService.GetParent(unit);
            }

            return string.Empty;
        }

        private string GetNextUnitCommanderExcludeSelf(DomainUnit unit)
        {
            while (unit != null)
            {
                if (UnitHasCommander(unit))
                {
                    string commander = GetCommander(unit);
                    if (commander != _httpContextService.GetUserId())
                    {
                        return commander;
                    }
                }

                unit = _unitsService.GetParent(unit);
            }

            return string.Empty;
        }

        private bool UnitHasCommander(DomainUnit unit)
        {
            return _unitsService.HasRole(unit, _rolesService.GetCommanderRoleName());
        }

        private string GetCommander(DomainUnit unit)
        {
            return unit.Roles.GetValueOrDefault(_rolesService.GetCommanderRoleName(), string.Empty);
        }
    }
}
