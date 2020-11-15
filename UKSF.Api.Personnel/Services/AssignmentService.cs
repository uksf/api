using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvsAnLib;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;

namespace UKSF.Api.Personnel.Services {
    public interface IAssignmentService {
        Task AssignUnitRole(string id, string unitId, string role);
        Task UnassignAllUnits(string id);
        Task UnassignAllUnitRoles(string id);
        Task<Notification> UpdateUnitRankAndRole(string id, string unitString = "", string role = "", string rankString = "", string notes = "", string message = "", string reason = "");
        Task<string> UnassignUnitRole(string id, string unitId);
        Task UnassignUnit(string id, string unitId);
        Task UpdateGroupsAndRoles(string id);
    }

    public class AssignmentService : IAssignmentService {
        public const string REMOVE_FLAG = "REMOVE";
        private readonly IEventBus<Account> _accountEventBus;
        private readonly IHubContext<AccountHub, IAccountClient> _accountHub;
        private readonly IAccountService _accountService;
        private readonly IDisplayNameService _displayNameService;
        private readonly IRanksService _ranksService;
        private readonly IServiceRecordService _serviceRecordService;
        private readonly IUnitsService _unitsService;

        public AssignmentService(
            IServiceRecordService serviceRecordService,
            IAccountService accountService,
            IRanksService ranksService,
            IUnitsService unitsService,
            IDisplayNameService displayNameService,
            IHubContext<AccountHub, IAccountClient> accountHub,
            IEventBus<Account> accountEventBus
        ) {
            _serviceRecordService = serviceRecordService;
            _accountService = accountService;
            _ranksService = ranksService;
            _unitsService = unitsService;
            _displayNameService = displayNameService;
            _accountHub = accountHub;
            _accountEventBus = accountEventBus;
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
                    message = $"{message}. Please change your name to {_displayNameService.GetDisplayName(id)}";
                }
            }

            _serviceRecordService.AddServiceRecord(id, message, notes);
            await UpdateGroupsAndRoles(id);
            return message != REMOVE_FLAG ? new Notification { owner = id, message = message, icon = positive ? NotificationIcons.PROMOTION : NotificationIcons.DEMOTION } : null;
        }

        public async Task AssignUnitRole(string id, string unitId, string role) {
            await _unitsService.SetMemberRole(id, unitId, role);
            await UpdateGroupsAndRoles(id);
        }

        public async Task UnassignAllUnits(string id) {
            foreach (Unit unit in _unitsService.Data.Get()) {
                await _unitsService.RemoveMember(id, unit);
            }

            await UpdateGroupsAndRoles(id);
        }

        public async Task UnassignAllUnitRoles(string id) {
            foreach (Unit unit in _unitsService.Data.Get()) {
                await _unitsService.SetMemberRole(id, unit);
            }

            await UpdateGroupsAndRoles(id);
        }

        public async Task<string> UnassignUnitRole(string id, string unitId) {
            Unit unit = _unitsService.Data.GetSingle(unitId);
            string role = unit.roles.FirstOrDefault(x => x.Value == id).Key;
            if (_unitsService.RolesHasMember(unit, id)) {
                await _unitsService.SetMemberRole(id, unitId);
                await UpdateGroupsAndRoles(id);
            }

            return role;
        }

        public async Task UnassignUnit(string id, string unitId) {
            Unit unit = _unitsService.Data.GetSingle(unitId);
            await _unitsService.RemoveMember(id, unit);
            await UpdateGroupsAndRoles(unitId);
        }

        // TODO: teamspeak and discord should probably be updated for account update events, or a separate assignment event bus could be used
        public async Task UpdateGroupsAndRoles(string id) {
            Account account = _accountService.Data.GetSingle(id);
            _accountEventBus.Send(account);
            await _accountHub.Clients.Group(id).ReceiveAccountUpdate();
        }

        private async Task<Tuple<bool, bool>> UpdateUnit(string id, string unitString, StringBuilder notificationMessage) {
            bool unitUpdate = false;
            bool positive = true;
            Unit unit = _unitsService.Data.GetSingle(x => x.name == unitString);
            if (unit != null) {
                if (unit.branch == UnitBranch.COMBAT) {
                    await _unitsService.RemoveMember(id, _accountService.Data.GetSingle(id).unitAssignment);
                    await _accountService.Data.Update(id, "unitAssignment", unit.name);
                }

                await _unitsService.AddMember(id, unit.id);
                notificationMessage.Append($"You have been transfered to {_unitsService.GetChainString(unit)}");
                unitUpdate = true;
            } else if (unitString == REMOVE_FLAG) {
                string currentUnit = _accountService.Data.GetSingle(id).unitAssignment;
                if (string.IsNullOrEmpty(currentUnit)) return new Tuple<bool, bool>(false, false);
                unit = _unitsService.Data.GetSingle(x => x.name == currentUnit);
                await _unitsService.RemoveMember(id, currentUnit);
                await _accountService.Data.Update(id, "unitAssignment", null);
                notificationMessage.Append($"You have been removed from {_unitsService.GetChainString(unit)}");
                unitUpdate = true;
                positive = false;
            }

            return new Tuple<bool, bool>(unitUpdate, positive);
        }

        private async Task<Tuple<bool, bool>> UpdateRole(string id, string role, bool unitUpdate, StringBuilder notificationMessage) {
            bool roleUpdate = false;
            bool positive = true;
            if (!string.IsNullOrEmpty(role) && role != REMOVE_FLAG) {
                await _accountService.Data.Update(id, "roleAssignment", role);
                notificationMessage.Append($"{(unitUpdate ? $" as {AvsAn.Query(role).Article} {role}" : $"You have been assigned as {AvsAn.Query(role).Article} {role}")}");
                roleUpdate = true;
            } else if (role == REMOVE_FLAG) {
                string currentRole = _accountService.Data.GetSingle(id).roleAssignment;
                await _accountService.Data.Update(id, "roleAssignment", null);
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
            string currentRank = _accountService.Data.GetSingle(id).rank;
            if (!string.IsNullOrEmpty(rank) && rank != REMOVE_FLAG) {
                if (currentRank == rank) return new Tuple<bool, bool>(false, true);
                await _accountService.Data.Update(id, "rank", rank);
                bool promotion = string.IsNullOrEmpty(currentRank) || _ranksService.IsSuperior(rank, currentRank);
                notificationMessage.Append(
                    $"{(unitUpdate || roleUpdate ? $" and {(promotion ? "promoted" : "demoted")} to {rank}" : $"You have been {(promotion ? "promoted" : "demoted")} to {rank}")}"
                );
                rankUpdate = true;
            } else if (rank == REMOVE_FLAG) {
                await _accountService.Data.Update(id, "rank", null);
                notificationMessage.Append($"{(unitUpdate || roleUpdate ? $" and demoted from {currentRank}" : $"You have been demoted from {currentRank}")}");
                rankUpdate = true;
                positive = false;
            }

            return new Tuple<bool, bool>(rankUpdate, positive);
        }
    }
}
