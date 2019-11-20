using System.Collections.Generic;
using System.Linq;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Models.Units;
using UKSFWebsite.Api.Services.Admin;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Integrations.Teamspeak {
    public class TeamspeakGroupService : ITeamspeakGroupService {
        private readonly IRanksService ranksService;
        private readonly IUnitsService unitsService;
        private readonly ITeamspeakManagerService teamspeakManagerService;

        public TeamspeakGroupService(IRanksService ranksService, IUnitsService unitsService, ITeamspeakManagerService teamspeakManagerService) {
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.teamspeakManagerService = teamspeakManagerService;
        }

        public void UpdateAccountGroups(Account account, ICollection<double> serverGroups, double clientDbId) {
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
                    RemoveServerGroup(clientDbId, serverGroup);
                }
            }

            foreach (double serverGroup in allowedGroups.Where(serverGroup => !serverGroups.Contains(serverGroup))) {
                AddServerGroup(clientDbId, serverGroup);
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

        private void AddServerGroup(double clientDbId, double serverGroup) {
            teamspeakManagerService.SendProcedure(TeamspeakProcedureType.ASSIGN, new {clientDbId, serverGroup});
        }

        private void RemoveServerGroup(double clientDbId, double serverGroup) {
            teamspeakManagerService.SendProcedure(TeamspeakProcedureType.UNASSIGN, new {clientDbId, serverGroup});
        }
    }
}
