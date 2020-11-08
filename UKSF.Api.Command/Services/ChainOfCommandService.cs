using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Services;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Command.Services {
    public interface IChainOfCommandService {
        HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, Unit start, Unit target);
        bool InContextChainOfCommand(string id);
    }

    public class ChainOfCommandService : IChainOfCommandService {
        private readonly string commanderRoleName;

        private readonly IUnitsService unitsService;
        private readonly IHttpContextService httpContextService;
        private readonly IAccountService accountService;

        public ChainOfCommandService(IUnitsService unitsService, IRolesService rolesService, IHttpContextService httpContextService, IAccountService accountService) {
            this.unitsService = unitsService;
            this.httpContextService = httpContextService;
            this.accountService = accountService;

            commanderRoleName = rolesService.GetUnitRoleByOrder(0).name;
        }

        public HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, Unit start, Unit target) {
            HashSet<string> chain = ResolveMode(mode, start, target).Where(x => x != recipient).ToHashSet();
            chain.CleanHashset();

            // If no chain, and mode is not next commander, get next commander
            if (chain.Count == 0 && mode != ChainOfCommandMode.NEXT_COMMANDER && mode != ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF) {
                chain = GetNextCommander(start).Where(x => x != recipient).ToHashSet();
                chain.CleanHashset();
            }

            // If no chain, get root unit commander
            if (chain.Count == 0) {
                chain.Add(GetCommander(unitsService.GetRoot()));
                chain.CleanHashset();
            }

            // If no chain, get root unit child commanders
            if (chain.Count == 0) {
                foreach (Unit unit in unitsService.Data.Get(x => x.parent == unitsService.GetRoot().id).Where(unit => UnitHasCommander(unit) && GetCommander(unit) != recipient)) {
                    chain.Add(GetCommander(unit));
                }
                chain.CleanHashset();
            }

            // If no chain, get personnel
            if (chain.Count == 0) {
                chain.UnionWith(GetPersonnel());
                chain.CleanHashset();
            }

            return chain;
        }

        public bool InContextChainOfCommand(string id) {
            Account contextAccount = accountService.GetUserAccount();
            if (id == contextAccount.id) return true;
            Unit unit = unitsService.Data.GetSingle(x => x.name == contextAccount.unitAssignment);
            return unitsService.RolesHasMember(unit, contextAccount.id) && (unit.members.Contains(id) || unitsService.GetAllChildren(unit, true).Any(unitChild => unitChild.members.Contains(id)));
        }

        private IEnumerable<string> ResolveMode(ChainOfCommandMode mode, Unit start, Unit target) {
            return mode switch {
                ChainOfCommandMode.FULL => Full(start),
                ChainOfCommandMode.NEXT_COMMANDER => GetNextCommander(start),
                ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF => GetNextCommanderExcludeSelf(start),
                ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE => CommanderAndOneAbove(start),
                ChainOfCommandMode.COMMANDER_AND_PERSONNEL => GetCommanderAndPersonnel(start),
                ChainOfCommandMode.COMMANDER_AND_TARGET_COMMANDER => GetCommanderAndTargetCommander(start, target),
                ChainOfCommandMode.PERSONNEL => GetPersonnel(),
                ChainOfCommandMode.TARGET_COMMANDER => GetNextCommander(target),
                _ => throw new InvalidOperationException("Chain of command mode not recognized")
            };
        }

        private IEnumerable<string> Full(Unit unit) {
            HashSet<string> chain = new HashSet<string>();
            while (unit != null) {
                if (UnitHasCommander(unit)) {
                    chain.Add(GetCommander(unit));
                }

                unit = unitsService.GetParent(unit);
            }

            return chain;
        }

        private IEnumerable<string> GetNextCommander(Unit unit) => new HashSet<string> {GetNextUnitCommander(unit)};

        private IEnumerable<string> GetNextCommanderExcludeSelf(Unit unit) => new HashSet<string> {GetNextUnitCommanderExcludeSelf(unit)};

        private IEnumerable<string> CommanderAndOneAbove(Unit unit) {
            HashSet<string> chain = new HashSet<string>();
            if (unit != null) {
                if (UnitHasCommander(unit)) {
                    chain.Add(GetCommander(unit));
                }

                Unit parentUnit = unitsService.GetParent(unit);
                if (parentUnit != null && UnitHasCommander(parentUnit)) {
                    chain.Add(GetCommander(parentUnit));
                }
            }

            return chain;
        }

        private IEnumerable<string> GetCommanderAndPersonnel(Unit unit) {
            HashSet<string> chain = new HashSet<string>();
            if (UnitHasCommander(unit)) {
                chain.Add(GetCommander(unit));
            }

            chain.UnionWith(GetPersonnel());
            return chain;
        }

        private IEnumerable<string> GetPersonnel() => unitsService.Data.GetSingle(x => x.shortname == "SR7").members.ToHashSet();

        private IEnumerable<string> GetCommanderAndTargetCommander(Unit unit, Unit targetUnit) => new HashSet<string> {GetNextUnitCommander(unit), GetNextUnitCommander(targetUnit)};

        private string GetNextUnitCommander(Unit unit) {
            while (unit != null) {
                if (UnitHasCommander(unit)) {
                    return GetCommander(unit);
                }

                unit = unitsService.GetParent(unit);
            }

            return string.Empty;
        }

        private string GetNextUnitCommanderExcludeSelf(Unit unit) {
            while (unit != null) {
                if (UnitHasCommander(unit)) {
                    string commander = GetCommander(unit);
                    if (commander != httpContextService.GetUserId()) return commander;
                }

                unit = unitsService.GetParent(unit);
            }

            return string.Empty;
        }

        private bool UnitHasCommander(Unit unit) => unitsService.HasRole(unit, commanderRoleName);

        private string GetCommander(Unit unit) => unit.roles.GetValueOrDefault(commanderRoleName, string.Empty);
    }
}
