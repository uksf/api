using System.Collections.Generic;
using System.Linq;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Teamspeak.Procedures;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Teamspeak {
    public class TeamspeakGroupService : ITeamspeakGroupService {
        private readonly IRanksService ranksService;
        private readonly IUnitsService unitsService;

        public TeamspeakGroupService(IRanksService ranksService, IUnitsService unitsService) {
            this.ranksService = ranksService;
            this.unitsService = unitsService;
        }

        public void UpdateAccountGroups(Account account, ICollection<string> serverGroups, string clientDbId) {
            HashSet<string> allowedGroups = new HashSet<string>();

            if (account == null || account.membershipState == MembershipState.UNCONFIRMED) {
                allowedGroups.Add(VariablesWrapper.VariablesService().GetSingle("TSGID_UNVERIFIED").AsString());
            }

            if (account?.membershipState == MembershipState.DISCHARGED) {
                allowedGroups.Add(VariablesWrapper.VariablesService().GetSingle("TSGID_DISCHARGED").AsString());
            }

            if (account != null) {
                UpdateRank(account, allowedGroups);
                UpdateUnits(account, allowedGroups);
            }

            string[] groupsBlacklist = VariablesWrapper.VariablesService().GetSingle("TSGID_BLACKLIST").AsArray();
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
            ranksService.Get()
                        .ForEach(
                            x => {
                                if (rank == x.name) {
                                    allowedGroups.Add(x.teamspeakGroup);
                                }
                            }
                        );
        }

        private void UpdateUnits(Account account, ISet<string> allowedGroups) {
            Unit accountUnit = unitsService.GetSingle(x => x.name == account.unitAssignment);
            List<Unit> accountUnits = unitsService.Get(x => x.members.Contains(account.id)).Where(x => !string.IsNullOrEmpty(x.teamspeakGroup)).ToList();
            List<Unit> accountUnitParents = unitsService.GetParents(accountUnit).Where(x => !string.IsNullOrEmpty(x.teamspeakGroup)).ToList();

            Unit elcom = unitsService.GetAuxilliaryRoot();
            if (elcom.members.Contains(account.id)) {
                accountUnits.Remove(accountUnits.Find(x => x.branch == UnitBranch.COMBAT));
                accountUnitParents = accountUnitParents.TakeLast(2).ToList();
                allowedGroups.Add(VariablesWrapper.VariablesService().GetSingle("TSGID_ELCOM").AsString());
            }

            accountUnits.ForEach(x => allowedGroups.Add(x.teamspeakGroup));
            accountUnitParents.ForEach(x => allowedGroups.Add(x.teamspeakGroup));
        }

        private static void AddServerGroup(string clientDbId, string serverGroup) {
            PipeQueueManager.QueueMessage($"{ProcedureDefinitons.PROC_ASSIGN_SERVER_GROUP}:{clientDbId}|{serverGroup}");
        }

        private static void RemoveServerGroup(string clientDbId, string serverGroup) {
            PipeQueueManager.QueueMessage($"{ProcedureDefinitons.PROC_UNASSIGN_SERVER_GROUP}:{clientDbId}|{serverGroup}");
        }
    }
}
