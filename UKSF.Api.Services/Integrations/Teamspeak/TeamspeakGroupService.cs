using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models.Integrations;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Units;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Utility;

namespace UKSF.Api.Services.Integrations.Teamspeak {
    public class TeamspeakGroupService : ITeamspeakGroupService {
        private readonly IRanksService ranksService;
        private readonly IUnitsService unitsService;
        private readonly ITeamspeakManagerService teamspeakManagerService;

        public TeamspeakGroupService(IRanksService ranksService, IUnitsService unitsService, ITeamspeakManagerService teamspeakManagerService) {
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.teamspeakManagerService = teamspeakManagerService;
        }

        public async Task UpdateAccountGroups(Account account, ICollection<double> serverGroups, double clientDbId) {
            HashSet<double> allowedGroups = new HashSet<double>();

            if (account == null || account.membershipState == MembershipState.UNCONFIRMED) {
                allowedGroups.Add(VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_GID_UNVERIFIED").AsDouble());
            }

            if (account?.membershipState == MembershipState.DISCHARGED) {
                allowedGroups.Add(VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_GID_DISCHARGED").AsDouble());
            }

            if (account != null) {
                UpdateRank(account, allowedGroups);
                UpdateUnits(account, allowedGroups);
            }

            List<double> groupsBlacklist = VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_GID_BLACKLIST").AsDoublesArray().ToList();
            foreach (double serverGroup in serverGroups) {
                if (!allowedGroups.Contains(serverGroup) && !groupsBlacklist.Contains(serverGroup)) {
                    await RemoveServerGroup(clientDbId, serverGroup);
                }
            }

            foreach (double serverGroup in allowedGroups.Where(serverGroup => !serverGroups.Contains(serverGroup))) {
                await AddServerGroup(clientDbId, serverGroup);
            }
        }

        private void UpdateRank(Account account, ISet<double> allowedGroups) {
            string rank = account.rank;
            foreach (Rank x in ranksService.Data().Get().Where(x => rank == x.name)) {
                allowedGroups.Add(x.teamspeakGroup.ToDouble());
            }
        }

        private void UpdateUnits(Account account, ISet<double> allowedGroups) {
            Unit accountUnit = unitsService.Data().GetSingle(x => x.name == account.unitAssignment);
            List<Unit> accountUnits = unitsService.Data().Get(x => x.members.Contains(account.id)).Where(x => !string.IsNullOrEmpty(x.teamspeakGroup)).ToList();
            List<Unit> accountUnitParents = unitsService.GetParents(accountUnit).Where(x => !string.IsNullOrEmpty(x.teamspeakGroup)).ToList();

            Unit elcom = unitsService.GetAuxilliaryRoot();
            if (elcom.members.Contains(account.id)) {
                accountUnits.Remove(accountUnits.Find(x => x.branch == UnitBranch.COMBAT));
                accountUnitParents = accountUnitParents.TakeLast(2).ToList();
                allowedGroups.Add(VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_GID_ELCOM").AsDouble());
            }

            accountUnits.ForEach(x => allowedGroups.Add(x.teamspeakGroup.ToDouble()));
            accountUnitParents.ForEach(x => allowedGroups.Add(x.teamspeakGroup.ToDouble()));
        }

        private async Task AddServerGroup(double clientDbId, double serverGroup) {
            await teamspeakManagerService.SendProcedure(TeamspeakProcedureType.ASSIGN, new {clientDbId, serverGroup});
        }

        private async Task RemoveServerGroup(double clientDbId, double serverGroup) {
            await teamspeakManagerService.SendProcedure(TeamspeakProcedureType.UNASSIGN, new {clientDbId, serverGroup});
        }
    }
}
