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
        private readonly ITeamspeakManager teamspeakManager;

        public TeamspeakGroupService(IRanksService ranksService, IUnitsService unitsService, ITeamspeakManager teamspeakManager) {
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.teamspeakManager = teamspeakManager;
        }

        public void UpdateAccountGroups(Account account, ICollection<string> serverGroups, string clientDbId) {
            HashSet<string> allowedGroups = new HashSet<string>();

            if (account == null || account.membershipState == MembershipState.UNCONFIRMED) {
                allowedGroups.Add(VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_GID_UNVERIFIED").AsString());
            }

            if (account?.membershipState == MembershipState.DISCHARGED) {
                allowedGroups.Add(VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_GID_DISCHARGED").AsString());
            }

            if (account != null) {
                UpdateRank(account, allowedGroups);
                UpdateUnits(account, allowedGroups);
            }

            string[] groupsBlacklist = VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_GID_BLACKLIST").AsArray();
            foreach (string serverGroup in serverGroups) {
                if (!allowedGroups.Contains(serverGroup) && !groupsBlacklist.Contains(serverGroup)) {
                    RemoveServerGroup(clientDbId, serverGroup);
                }
            }

            foreach (string serverGroup in allowedGroups.Where(serverGroup => !serverGroups.Contains(serverGroup))) {
                AddServerGroup(clientDbId, serverGroup);
            }
        }

        private void UpdateRank(Account account, ISet<string> allowedGroups) {
            string rank = account.rank;
            foreach (Rank x in ranksService.Data().Get().Where(x => rank == x.name)) {
                allowedGroups.Add(x.teamspeakGroup);
            }
        }

        private void UpdateUnits(Account account, ISet<string> allowedGroups) {
            Unit accountUnit = unitsService.Data().GetSingle(x => x.name == account.unitAssignment);
            List<Unit> accountUnits = unitsService.Data().Get(x => x.members.Contains(account.id)).Where(x => !string.IsNullOrEmpty(x.teamspeakGroup)).ToList();
            List<Unit> accountUnitParents = unitsService.GetParents(accountUnit).Where(x => !string.IsNullOrEmpty(x.teamspeakGroup)).ToList();

            Unit elcom = unitsService.GetAuxilliaryRoot();
            if (elcom.members.Contains(account.id)) {
                accountUnits.Remove(accountUnits.Find(x => x.branch == UnitBranch.COMBAT));
                accountUnitParents = accountUnitParents.TakeLast(2).ToList();
                allowedGroups.Add(VariablesWrapper.VariablesDataService().GetSingle("TEAMSPEAK_GID_ELCOM").AsString());
            }

            accountUnits.ForEach(x => allowedGroups.Add(x.teamspeakGroup));
            accountUnitParents.ForEach(x => allowedGroups.Add(x.teamspeakGroup));
        }

        private void AddServerGroup(string clientDbId, string serverGroup) {
            teamspeakManager.SendProcedure($"{TeamspeakSocketProcedureType.ASSIGN}:{clientDbId}|{serverGroup}");
        }

        private void RemoveServerGroup(string clientDbId, string serverGroup) {
            teamspeakManager.SendProcedure($"{TeamspeakSocketProcedureType.UNASSIGN}:{clientDbId}|{serverGroup}");
        }
    }
}
