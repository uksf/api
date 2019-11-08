using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvsAnLib;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services {
    public class AssignmentService : IAssignmentService {
        public const string REMOVE_FLAG = "REMOVE";
        private readonly IAccountService accountService;
        private readonly IDisplayNameService displayNameService;
        private readonly IRanksService ranksService;
        private readonly IServerService serverService;
        private readonly IServiceRecordService serviceRecordService;
        private readonly ITeamspeakService teamspeakService;
        private readonly IUnitsService unitsService;
        private readonly IDiscordService discordService;
        private readonly IHubContext<AccountHub, IAccountClient> accountHub;

        public AssignmentService(
            IServiceRecordService serviceRecordService,
            IAccountService accountService,
            IRanksService ranksService,
            IUnitsService unitsService,
            ITeamspeakService teamspeakService,
            IServerService serverService,
            IDisplayNameService displayNameService,
            IDiscordService discordService,
            IHubContext<AccountHub, IAccountClient> accountHub
        ) {
            this.serviceRecordService = serviceRecordService;
            this.accountService = accountService;
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.teamspeakService = teamspeakService;
            this.serverService = serverService;
            this.displayNameService = displayNameService;
            this.discordService = discordService;
            this.accountHub = accountHub;
        }

        public async Task<Notification> UpdateUnitRankAndRole(string id, string unitString = "", string role = "", string rankString = "", string notes = "", string message = "", string reason = "") {
            StringBuilder notificationBuilder = new StringBuilder();

            (bool unitUpdate, bool unitPositive) = await UpdateUnit(id, unitString, notificationBuilder);
            (bool roleUpdate, bool rolePositive) = await UpdateRole(id, role, unitUpdate, notificationBuilder);
            (bool rankUpdate, bool rankPositive) = await UpdateRank(id, rankString, unitUpdate, roleUpdate, notificationBuilder);
            bool positive;
            if (rankPositive) {
                positive = true;
            } else {
                positive = unitPositive || rolePositive;
            }

            if (!unitUpdate && !roleUpdate && !rankUpdate) return null;
            if (string.IsNullOrEmpty(message)) {
                message = notificationBuilder.ToString();
                if (!string.IsNullOrEmpty(reason)) {
                    message = $"{message} because {reason}";
                }

                if (rankUpdate) {
                    message = $"{message}. Please change your name to {displayNameService.GetDisplayName(id)}";
                }
            }

            serviceRecordService.AddServiceRecord(id, message, notes);
            await UpdateGroupsAndRoles(id);
            return message != REMOVE_FLAG ? new Notification {owner = id, message = message, icon = positive ? NotificationIcons.PROMOTION : NotificationIcons.DEMOTION} : null;
        }

        public async Task AssignUnitRole(string id, string unitId, string role) {
            await unitsService.SetMemberRole(id, unitId, role);
            await UpdateGroupsAndRoles(id);
        }

        public async Task UnassignAllUnits(string id) {
            foreach (Unit unit in unitsService.Get()) {
                await unitsService.RemoveMember(id, unit);
            }

            await UpdateGroupsAndRoles(id);
        }

        public async Task UnassignAllUnitRoles(string id) {
            foreach (Unit unit in unitsService.Get()) {
                await unitsService.SetMemberRole(id, unit);
            }

            await UpdateGroupsAndRoles(id);
        }

        public async Task<string> UnassignUnitRole(string id, string unitId) {
            Unit unit = unitsService.GetSingle(unitId);
            string role = unit.roles.FirstOrDefault(x => x.Value == id).Key;
            if (unitsService.RolesHasMember(unit, id)) {
                await unitsService.SetMemberRole(id, unitId);
                await UpdateGroupsAndRoles(id);
            }

            return role;
        }

        public async Task UnassignUnit(string id, string unitId) {
            Unit unit = unitsService.GetSingle(unitId);
            await unitsService.RemoveMember(id, unit);
            await UpdateGroupsAndRoles(unitId);
        }

        private async Task UpdateGroupsAndRoles(string id) {
            Account account = accountService.GetSingle(id);
            teamspeakService.UpdateAccountTeamspeakGroups(account);
            await discordService.UpdateAccount(account);
            serverService.UpdateSquadXml();
            await accountHub.Clients.Group(id).ReceiveAccountUpdate();
        }

        private async Task<Tuple<bool, bool>> UpdateUnit(string id, string unitString, StringBuilder notificationMessage) {
            bool unitUpdate = false;
            bool positive = true;
            Unit unit = unitsService.GetSingle(x => x.name == unitString);
            if (unit != null) {
                if (unit.branch == UnitBranch.COMBAT) {
                    await unitsService.RemoveMember(id, accountService.GetSingle(id).unitAssignment);
                    await accountService.Update(id, "unitAssignment", unit.name);
                }

                await unitsService.AddMember(id, unit.id);
                notificationMessage.Append($"You have been transfered to {unitsService.GetChainString(unit)}");
                unitUpdate = true;
            } else if (unitString == REMOVE_FLAG) {
                string currentUnit = accountService.GetSingle(id).unitAssignment;
                if (string.IsNullOrEmpty(currentUnit)) return new Tuple<bool, bool>(false, false);
                unit = unitsService.GetSingle(x => x.name == currentUnit);
                await unitsService.RemoveMember(id, currentUnit);
                await accountService.Update(id, "unitAssignment", null);
                notificationMessage.Append($"You have been removed from {unitsService.GetChainString(unit)}");
                unitUpdate = true;
                positive = false;
            }

            return new Tuple<bool, bool>(unitUpdate, positive);
        }

        private async Task<Tuple<bool, bool>> UpdateRole(string id, string role, bool unitUpdate, StringBuilder notificationMessage) {
            bool roleUpdate = false;
            bool positive = true;
            if (!string.IsNullOrEmpty(role) && role != REMOVE_FLAG) {
                await accountService.Update(id, "roleAssignment", role);
                notificationMessage.Append($"{(unitUpdate ? $" as {AvsAn.Query(role).Article} {role}" : $"You have been assigned as {AvsAn.Query(role).Article} {role}")}");
                roleUpdate = true;
            } else if (role == REMOVE_FLAG) {
                string currentRole = accountService.GetSingle(id).roleAssignment;
                await accountService.Update(id, "roleAssignment", null);
                notificationMessage.Append(
                    string.IsNullOrEmpty(currentRole)
                        ? $"{(unitUpdate ? " and unassigned from your role" : "You have been unassigned from your role")}"
                        : $"{(unitUpdate ? $" and unassigned as {AvsAn.Query(currentRole).Article} {currentRole}" : $"You have been unassigned as {AvsAn.Query(currentRole).Article} {currentRole}")}"
                );

                roleUpdate = true;
                positive = false;
            }

            return new Tuple<bool, bool>(roleUpdate, positive);
        }

        private async Task<Tuple<bool, bool>> UpdateRank(string id, string rank, bool unitUpdate, bool roleUpdate, StringBuilder notificationMessage) {
            bool rankUpdate = false;
            bool positive = true;
            string currentRank = accountService.GetSingle(id).rank;
            if (!string.IsNullOrEmpty(rank) && rank != REMOVE_FLAG) {
                if (currentRank == rank) return new Tuple<bool, bool>(false, true);
                await accountService.Update(id, "rank", rank);
                bool promotion = string.IsNullOrEmpty(currentRank) || ranksService.IsSuperior(rank, currentRank);
                notificationMessage.Append($"{(unitUpdate || roleUpdate ? $" and {(promotion ? "promoted" : "demoted")} to {rank}" : $"You have been {(promotion ? "promoted" : "demoted")} to {rank}")}");
                rankUpdate = true;
            } else if (rank == REMOVE_FLAG) {
                await accountService.Update(id, "rank", null);
                notificationMessage.Append($"{(unitUpdate || roleUpdate ? $" and demoted from {currentRank}" : $"You have been demoted from {currentRank}")}");
                rankUpdate = true;
                positive = false;
            }

            return new Tuple<bool, bool>(rankUpdate, positive);
        }
    }
}
