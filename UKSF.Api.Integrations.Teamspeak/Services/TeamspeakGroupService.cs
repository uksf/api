using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Teamspeak.Models;

namespace UKSF.Api.Teamspeak.Services {
    public interface ITeamspeakGroupService {
        Task UpdateAccountGroups(Account account, ICollection<double> serverGroups, double clientDbId);
    }

    public class TeamspeakGroupService : ITeamspeakGroupService {
        private readonly IRanksService ranksService;
        private readonly ITeamspeakManagerService teamspeakManagerService;
        private readonly IUnitsService unitsService;
        private readonly IVariablesService variablesService;

        public TeamspeakGroupService(IRanksService ranksService, IUnitsService unitsService, ITeamspeakManagerService teamspeakManagerService, IVariablesService variablesService) {
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.teamspeakManagerService = teamspeakManagerService;
            this.variablesService = variablesService;
        }

        public async Task UpdateAccountGroups(Account account, ICollection<double> serverGroups, double clientDbId) {
            HashSet<double> memberGroups = new HashSet<double>();

            if (account == null) {
                memberGroups.Add(variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsDouble());
            } else {
                switch (account.membershipState) {
                    case MembershipState.UNCONFIRMED:
                        memberGroups.Add(variablesService.GetVariable("TEAMSPEAK_GID_UNVERIFIED").AsDouble());
                        break;
                    case MembershipState.DISCHARGED:
                        memberGroups.Add(variablesService.GetVariable("TEAMSPEAK_GID_DISCHARGED").AsDouble());
                        break;
                    case MembershipState.CONFIRMED:
                        ResolveRankGroup(account, memberGroups);
                        break;
                    case MembershipState.MEMBER:
                        ResolveRankGroup(account, memberGroups);
                        ResolveUnitGroup(account, memberGroups);
                        ResolveParentUnitGroup(account, memberGroups);
                        ResolveAuxiliaryUnitGroups(account, memberGroups);
                        memberGroups.Add(variablesService.GetVariable("TEAMSPEAK_GID_ROOT").AsDouble());
                        break;
                }
            }

            List<double> groupsBlacklist = variablesService.GetVariable("TEAMSPEAK_GID_BLACKLIST").AsDoublesArray().ToList();
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
            memberGroups.Add(ranksService.Data.GetSingle(account.rank).teamspeakGroup.ToDouble());
        }

        private void ResolveUnitGroup(Account account, ISet<double> memberGroups) {
            Unit accountUnit = unitsService.Data.GetSingle(x => x.name == account.unitAssignment);
            Unit elcom = unitsService.GetAuxilliaryRoot();

            if (accountUnit.parent == ObjectId.Empty.ToString()) {
                memberGroups.Add(accountUnit.teamspeakGroup.ToDouble());
            }

            double group = elcom.members.Contains(account.id) ? variablesService.GetVariable("TEAMSPEAK_GID_ELCOM").AsDouble() : accountUnit.teamspeakGroup.ToDouble();
            if (group == 0) {
                ResolveParentUnitGroup(account, memberGroups);
            } else {
                memberGroups.Add(group);
            }
        }

        private void ResolveParentUnitGroup(Account account, ISet<double> memberGroups) {
            Unit accountUnit = unitsService.Data.GetSingle(x => x.name == account.unitAssignment);
            Unit parentUnit = unitsService.GetParents(accountUnit).Skip(1).FirstOrDefault(x => !string.IsNullOrEmpty(x.teamspeakGroup) && !memberGroups.Contains(x.teamspeakGroup.ToDouble()));
            if (parentUnit != null && parentUnit.parent != ObjectId.Empty.ToString()) {
                memberGroups.Add(parentUnit.teamspeakGroup.ToDouble());
            } else {
                memberGroups.Add(accountUnit.teamspeakGroup.ToDouble());
            }
        }

        private void ResolveAuxiliaryUnitGroups(DatabaseObject account, ISet<double> memberGroups) {
            IEnumerable<Unit> accountUnits = unitsService.Data.Get(x => x.parent != ObjectId.Empty.ToString() && x.branch == UnitBranch.AUXILIARY && x.members.Contains(account.id))
                                                         .Where(x => !string.IsNullOrEmpty(x.teamspeakGroup));
            foreach (Unit unit in accountUnits) {
                memberGroups.Add(unit.teamspeakGroup.ToDouble());
            }
        }

        private async Task AddServerGroup(double clientDbId, double serverGroup) {
            await teamspeakManagerService.SendGroupProcedure(TeamspeakProcedureType.ASSIGN, new TeamspeakGroupProcedure { clientDbId = clientDbId, serverGroup = serverGroup });
        }

        private async Task RemoveServerGroup(double clientDbId, double serverGroup) {
            await teamspeakManagerService.SendGroupProcedure(TeamspeakProcedureType.UNASSIGN, new TeamspeakGroupProcedure { clientDbId = clientDbId, serverGroup = serverGroup });
        }
    }
}
