using System;
using System.Collections.Generic;
using System.Linq;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services {
    public enum ChainOfCommandMode {
        FULL,
        NEXT_COMMANDER,
        NEXT_COMMANDER_EXCLUDE_SELF,
        COMMANDER_AND_ONE_ABOVE,
        COMMANDER_AND_SR10,
        COMMANDER_AND_TARGET_COMMANDER,
        SR10,
        TARGET_COMMANDER
    }

    public class ChainOfCommandService : IChainOfCommandService {
        private readonly string commanderRoleName;
        private readonly ISessionService sessionService;
        private readonly IUnitsService unitsService;

        public ChainOfCommandService(IUnitsService unitsService, IRolesService rolesService, ISessionService sessionService) {
            this.unitsService = unitsService;
            this.sessionService = sessionService;
            commanderRoleName = rolesService.GetUnitRoleByOrder(0).name;
        }

        public HashSet<string> ResolveChain(ChainOfCommandMode mode, Unit start, Unit target) {
            HashSet<string> chain = ResolveMode(mode, start, target).Where(x => !string.IsNullOrEmpty(x)).ToHashSet();
            if (chain.Count == 0 && mode != ChainOfCommandMode.NEXT_COMMANDER && mode != ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF) {
                chain = GetNextCommander(start).Where(x => !string.IsNullOrEmpty(x)).ToHashSet();
            }

            if (chain.Count == 0) {
                foreach (Unit unit in unitsService.Get(x => x.parent == unitsService.GetRoot().id)) {
                    if (UnitHasCommander(unit)) {
                        chain.Add(GetCommander(unit));
                    }
                }
            }

            return chain;
        }

        public bool InContextChainOfCommand(string id) {
            Account contextAccount = sessionService.GetContextAccount();
            if (id == contextAccount.id) return true;
            Unit unit = unitsService.GetSingle(x => x.name == contextAccount.unitAssignment);
            return unitsService.RolesHasMember(unit, contextAccount.id) && (unit.members.Contains(id) || unitsService.GetAllChildren(unit, true).Any(unitChild => unitChild.members.Contains(id)));
        }

        private IEnumerable<string> ResolveMode(ChainOfCommandMode mode, Unit start, Unit target) {
            switch (mode) {
                case ChainOfCommandMode.FULL: return Full(start);
                case ChainOfCommandMode.NEXT_COMMANDER: return GetNextCommander(start);
                case ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF: return GetNextCommanderExcludeSelf(start);
                case ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE: return CommanderAndOneAbove(start);
                case ChainOfCommandMode.COMMANDER_AND_SR10: return GetCommanderAndSr10(start);
                case ChainOfCommandMode.COMMANDER_AND_TARGET_COMMANDER: return GetCommanderAndTargetCommander(start, target);
                case ChainOfCommandMode.SR10: return GetSr10();
                case ChainOfCommandMode.TARGET_COMMANDER: return GetNextCommander(target);
                default: throw new InvalidOperationException("Chain of command mode not recognized");
            }
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

        private HashSet<string> GetNextCommander(Unit unit) => new HashSet<string> {GetNextUnitCommander(unit)};

        private HashSet<string> GetNextCommanderExcludeSelf(Unit unit) => new HashSet<string> {GetNextUnitCommanderExcludeSelf(unit)};

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

        private IEnumerable<string> GetCommanderAndSr10(Unit unit) {
            HashSet<string> chain = new HashSet<string>();
            if (UnitHasCommander(unit)) {
                chain.Add(GetCommander(unit));
            }

            chain.UnionWith(GetSr10());
            return chain;
        }

        private IEnumerable<string> GetSr10() => unitsService.GetSingle(x => x.shortname == "SR10").members.ToHashSet();

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
                    if (commander != sessionService.GetContextId()) return commander;
                }

                unit = unitsService.GetParent(unit);
            }

            return string.Empty;
        }

        private bool UnitHasCommander(Unit unit) => unitsService.HasRole(unit, commanderRoleName);

        private string GetCommander(Unit unit) => unit.roles.GetValueOrDefault(commanderRoleName, string.Empty);
    }
}
