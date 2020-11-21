using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Teamspeak.Models;

namespace UKSF.Api.Teamspeak.Services {
    public interface ITeamspeakGroupService {
        Task UpdateAccountGroups(Account account, ICollection<double> serverGroups, double clientDbId);
    }

    public class TeamspeakGroupService : ITeamspeakGroupService {
        private readonly IRanksContext _ranksContext;
        private readonly ITeamspeakManagerService _teamspeakManagerService;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;
        private readonly IVariablesService _variablesService;

        public TeamspeakGroupService(
            IRanksContext ranksContext,
            IUnitsContext unitsContext,
            IUnitsService unitsService,
            ITeamspeakManagerService teamspeakManagerService,
            IVariablesService variablesService
        ) {
            _ranksContext = ranksContext;
            _unitsContext = unitsContext;
            _unitsService = unitsService;
            _teamspeakManagerService = teamspeakManagerService;
            _variablesService = variablesService;
        }

        public async Task UpdateAccountGroups(Account account, ICollection<double> serverGroups, double clientDbId) {
            HashSet<double> memberGroups = new();

            if (account == null) {
                memberGroups.Add(_variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsDouble());
            } else {
                switch (account.MembershipState) {
                    case MembershipState.UNCONFIRMED:
                        memberGroups.Add(_variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsDouble());
                        break;
                    case MembershipState.DISCHARGED:
                        memberGroups.Add(_variablesService.GetVariable("TEAMSPEAK_GID_DISCHARGED").AsDouble());
                        break;
                    case MembershipState.CONFIRMED:
                        ResolveRankGroup(account, memberGroups);
                        break;
                    case MembershipState.MEMBER:
                        ResolveRankGroup(account, memberGroups);
                        ResolveUnitGroup(account, memberGroups);
                        ResolveParentUnitGroup(account, memberGroups);
                        ResolveAuxiliaryUnitGroups(account, memberGroups);
                        memberGroups.Add(_variablesService.GetVariable("TEAMSPEAK_GID_ROOT").AsDouble());
                        break;
                }
            }

            List<double> groupsBlacklist = _variablesService.GetVariable("TEAMSPEAK_GID_BLACKLIST").AsDoublesArray().ToList();
            foreach (double serverGroup in serverGroups) {
                if (!memberGroups.Contains(serverGroup) && !groupsBlacklist.Contains(serverGroup)) {
                    await RemoveServerGroup(clientDbId, serverGroup);
                }
            }

            foreach (double serverGroup in memberGroups.Where(serverGroup => !serverGroups.Contains(serverGroup))) {
                await AddServerGroup(clientDbId, serverGroup);
            }
        }

        private void ResolveRankGroup(Account account, ISet<double> memberGroups) {
            memberGroups.Add(_ranksContext.GetSingle(account.Rank).TeamspeakGroup.ToDouble());
        }

        private void ResolveUnitGroup(Account account, ISet<double> memberGroups) {
            Unit accountUnit = _unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
            Unit elcom = _unitsService.GetAuxilliaryRoot();

            if (accountUnit.Parent == ObjectId.Empty.ToString()) {
                memberGroups.Add(accountUnit.TeamspeakGroup.ToDouble());
            }

            double group = elcom.Members.Contains(account.Id) ? _variablesService.GetVariable("TEAMSPEAK_GID_ELCOM").AsDouble() : accountUnit.TeamspeakGroup.ToDouble();
            if (group == 0) {
                ResolveParentUnitGroup(account, memberGroups);
            } else {
                memberGroups.Add(group);
            }
        }

        private void ResolveParentUnitGroup(Account account, ISet<double> memberGroups) {
            Unit accountUnit = _unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
            Unit parentUnit = _unitsService.GetParents(accountUnit).Skip(1).FirstOrDefault(x => !string.IsNullOrEmpty(x.TeamspeakGroup) && !memberGroups.Contains(x.TeamspeakGroup.ToDouble()));
            if (parentUnit != null && parentUnit.Parent != ObjectId.Empty.ToString()) {
                memberGroups.Add(parentUnit.TeamspeakGroup.ToDouble());
            } else {
                memberGroups.Add(accountUnit.TeamspeakGroup.ToDouble());
            }
        }

        private void ResolveAuxiliaryUnitGroups(MongoObject account, ISet<double> memberGroups) {
            IEnumerable<Unit> accountUnits = _unitsContext.Get(x => x.Parent != ObjectId.Empty.ToString() && x.Branch == UnitBranch.AUXILIARY && x.Members.Contains(account.Id))
                                                          .Where(x => !string.IsNullOrEmpty(x.TeamspeakGroup));
            foreach (Unit unit in accountUnits) {
                memberGroups.Add(unit.TeamspeakGroup.ToDouble());
            }
        }

        private async Task AddServerGroup(double clientDbId, double serverGroup) {
            await _teamspeakManagerService.SendGroupProcedure(TeamspeakProcedureType.ASSIGN, new TeamspeakGroupProcedure { ClientDbId = clientDbId, ServerGroup = serverGroup });
        }

        private async Task RemoveServerGroup(double clientDbId, double serverGroup) {
            await _teamspeakManagerService.SendGroupProcedure(TeamspeakProcedureType.UNASSIGN, new TeamspeakGroupProcedure { ClientDbId = clientDbId, ServerGroup = serverGroup });
        }
    }
}
