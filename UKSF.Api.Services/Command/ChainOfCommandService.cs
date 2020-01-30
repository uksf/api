using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Command;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Units;

namespace UKSF.Api.Services.Command {
    public class ChainOfCommandService : IChainOfCommandService {
        private readonly string commanderRoleName;
        private readonly ISessionService sessionService;
        private readonly IUnitsService unitsService;

        public ChainOfCommandService(IUnitsService unitsService, IRolesService rolesService, ISessionService sessionService) {
            this.unitsService = unitsService;
            this.sessionService = sessionService;
            commanderRoleName = rolesService.GetUnitRoleByOrder(0).name;
        }

        public HashSet<string> ResolveChain(ChainOfCommandMode mode, string recipient, Unit start, Unit target) {
            HashSet<string> chain = ResolveMode(mode, start, target).Where(x => !string.IsNullOrEmpty(x) && x != recipient).ToHashSet();

            // If no chain, and mode is not next commander, get next commander
            if (chain.Count == 0 && mode != ChainOfCommandMode.NEXT_COMMANDER && mode != ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF) {
                chain = GetNextCommander(start).Where(x => !string.IsNullOrEmpty(x) && x != recipient).ToHashSet();
            }

            // If no chain, get root unit child commanders
            if (chain.Count == 0) {
                foreach (Unit unit in unitsService.Data().Get(x => x.parent == unitsService.GetRoot().id).Where(unit => UnitHasCommander(unit) && GetCommander(unit) != recipient)) {
                    chain.Add(GetCommander(unit));
                }
            }

            // If no chain, get root unit commander
            if (chain.Count == 0) {
                chain.Add(GetCommander(unitsService.GetRoot()));
            }

            return chain.Where(x => !string.IsNullOrEmpty(x)).ToHashSet();
        }

        public bool InContextChainOfCommand(string id) {
            Account contextAccount = sessionService.GetContextAccount();
            if (id == contextAccount.id) return true;
            Unit unit = unitsService.Data().GetSingle(x => x.name == contextAccount.unitAssignment);
            return unitsService.RolesHasMember(unit, contextAccount.id) && (unit.members.Contains(id) || unitsService.GetAllChildren(unit, true).Any(unitChild => unitChild.members.Contains(id)));
        }

        private IEnumerable<string> ResolveMode(ChainOfCommandMode mode, Unit start, Unit target) {
            return mode switch {
                ChainOfCommandMode.FULL => Full(start),
                ChainOfCommandMode.NEXT_COMMANDER => GetNextCommander(start),
                ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF => GetNextCommanderExcludeSelf(start),
                ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE => CommanderAndOneAbove(start),
                ChainOfCommandMode.COMMANDER_AND_SR10 => GetCommanderAndSr10(start),
                ChainOfCommandMode.COMMANDER_AND_TARGET_COMMANDER => GetCommanderAndTargetCommander(start, target),
                ChainOfCommandMode.SR10 => GetSr10(),
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

        private IEnumerable<string> GetCommanderAndSr10(Unit unit) {
            HashSet<string> chain = new HashSet<string>();
            if (UnitHasCommander(unit)) {
                chain.Add(GetCommander(unit));
            }

            chain.UnionWith(GetSr10());
            return chain;
        }

        private IEnumerable<string> GetSr10() => unitsService.Data().GetSingle(x => x.shortname == "SR10").members.ToHashSet();

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
