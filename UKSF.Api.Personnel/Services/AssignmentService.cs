using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvsAnLib;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;

namespace UKSF.Api.Personnel.Services
{
    public interface IAssignmentService
    {
        Task AssignUnitRole(string id, string unitId, string role);
        Task UnassignAllUnits(string id);
        Task UnassignAllUnitRoles(string id);

        Task<Notification> UpdateUnitRankAndRole(
            string id,
            string unitString = "",
            string role = "",
            string rankString = "",
            string notes = "",
            string message = "",
            string reason = ""
        );

        Task<string> UnassignUnitRole(string id, string unitId);
        Task UnassignUnit(string id, string unitId);
        Task UpdateGroupsAndRoles(string id);
    }

    public class AssignmentService : IAssignmentService
    {
        public const string REMOVE_FLAG = "REMOVE";
        private readonly IAccountContext _accountContext;
        private readonly IHubContext<AccountHub, IAccountClient> _accountHub;
        private readonly IDisplayNameService _displayNameService;
        private readonly IEventBus _eventBus;
        private readonly IRanksService _ranksService;
        private readonly IServiceRecordService _serviceRecordService;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;

        public AssignmentService(
            IAccountContext accountContext,
            IUnitsContext unitsContext,
            IServiceRecordService serviceRecordService,
            IRanksService ranksService,
            IUnitsService unitsService,
            IDisplayNameService displayNameService,
            IHubContext<AccountHub, IAccountClient> accountHub,
            IEventBus eventBus
        )
        {
            _accountContext = accountContext;
            _unitsContext = unitsContext;
            _serviceRecordService = serviceRecordService;
            _ranksService = ranksService;
            _unitsService = unitsService;
            _displayNameService = displayNameService;
            _accountHub = accountHub;
            _eventBus = eventBus;
        }

        public async Task<Notification> UpdateUnitRankAndRole(
            string id,
            string unitString = "",
            string role = "",
            string rankString = "",
            string notes = "",
            string message = "",
            string reason = ""
        )
        {
            StringBuilder notificationBuilder = new();

            (bool unitUpdate, bool unitPositive) = await UpdateUnit(id, unitString, notificationBuilder);
            (bool roleUpdate, bool rolePositive) = await UpdateRole(id, role, unitUpdate, notificationBuilder);
            (bool rankUpdate, bool rankPositive) = await UpdateRank(id, rankString, unitUpdate, roleUpdate, notificationBuilder);
            bool positive;
            if (rankPositive)
            {
                positive = true;
            }
            else
            {
                positive = unitPositive || rolePositive;
            }

            if (!unitUpdate && !roleUpdate && !rankUpdate)
            {
                return null;
            }

            if (string.IsNullOrEmpty(message))
            {
                message = notificationBuilder.ToString();
                if (!string.IsNullOrEmpty(reason))
                {
                    message = $"{message} because {reason}";
                }

                if (rankUpdate)
                {
                    message = $"{message}. Please change your name to {_displayNameService.GetDisplayName(id)}";
                }
            }

            _serviceRecordService.AddServiceRecord(id, message, notes);
            await UpdateGroupsAndRoles(id);
            return message != REMOVE_FLAG
                ? new Notification { Owner = id, Message = message, Icon = positive ? NotificationIcons.PROMOTION : NotificationIcons.DEMOTION }
                : null;
        }

        public async Task AssignUnitRole(string id, string unitId, string role)
        {
            await _unitsService.SetMemberRole(id, unitId, role);
            await UpdateGroupsAndRoles(id);
        }

        public async Task UnassignAllUnits(string id)
        {
            foreach (var unit in _unitsContext.Get())
            {
                await _unitsService.RemoveMember(id, unit);
            }

            await UpdateGroupsAndRoles(id);
        }

        public async Task UnassignAllUnitRoles(string id)
        {
            foreach (var unit in _unitsContext.Get())
            {
                await _unitsService.SetMemberRole(id, unit);
            }

            await UpdateGroupsAndRoles(id);
        }

        public async Task<string> UnassignUnitRole(string id, string unitId)
        {
            var unit = _unitsContext.GetSingle(unitId);
            string role = unit.Roles.FirstOrDefault(x => x.Value == id).Key;
            if (_unitsService.RolesHasMember(unit, id))
            {
                await _unitsService.SetMemberRole(id, unitId);
                await UpdateGroupsAndRoles(id);
            }

            return role;
        }

        public async Task UnassignUnit(string id, string unitId)
        {
            var unit = _unitsContext.GetSingle(unitId);
            await _unitsService.RemoveMember(id, unit);
            await UpdateGroupsAndRoles(unitId);
        }

        // TODO: teamspeak and discord should probably be updated for account update events, or a separate assignment event bus could be used
        public async Task UpdateGroupsAndRoles(string id)
        {
            DomainAccount domainAccount = _accountContext.GetSingle(id);
            _eventBus.Send(domainAccount);
            await _accountHub.Clients.Group(id).ReceiveAccountUpdate();
        }

        private async Task<Tuple<bool, bool>> UpdateUnit(string id, string unitString, StringBuilder notificationMessage)
        {
            bool unitUpdate = false;
            bool positive = true;
            var unit = _unitsContext.GetSingle(x => x.Name == unitString);
            if (unit != null)
            {
                if (unit.Branch == UnitBranch.COMBAT)
                {
                    await _unitsService.RemoveMember(id, _accountContext.GetSingle(id).UnitAssignment);
                    await _accountContext.Update(id, x => x.UnitAssignment, unit.Name);
                }

                await _unitsService.AddMember(id, unit.Id);
                notificationMessage.Append($"You have been transfered to {_unitsService.GetChainString(unit)}");
                unitUpdate = true;
            }
            else if (unitString == REMOVE_FLAG)
            {
                string currentUnit = _accountContext.GetSingle(id).UnitAssignment;
                if (string.IsNullOrEmpty(currentUnit))
                {
                    return new(false, false);
                }

                unit = _unitsContext.GetSingle(x => x.Name == currentUnit);
                await _unitsService.RemoveMember(id, currentUnit);
                await _accountContext.Update(id, x => x.UnitAssignment, null);
                notificationMessage.Append($"You have been removed from {_unitsService.GetChainString(unit)}");
                unitUpdate = true;
                positive = false;
            }

            return new(unitUpdate, positive);
        }

        private async Task<Tuple<bool, bool>> UpdateRole(string id, string role, bool unitUpdate, StringBuilder notificationMessage)
        {
            bool roleUpdate = false;
            bool positive = true;
            if (!string.IsNullOrEmpty(role) && role != REMOVE_FLAG)
            {
                await _accountContext.Update(id, x => x.RoleAssignment, role);
                notificationMessage.Append(
                    $"{(unitUpdate ? $" as {AvsAn.Query(role).Article} {role}" : $"You have been assigned as {AvsAn.Query(role).Article} {role}")}"
                );
                roleUpdate = true;
            }
            else if (role == REMOVE_FLAG)
            {
                string currentRole = _accountContext.GetSingle(id).RoleAssignment;
                await _accountContext.Update(id, x => x.RoleAssignment, null);
                notificationMessage.Append(
                    string.IsNullOrEmpty(currentRole)
                        ? $"{(unitUpdate ? " and unassigned from your role" : "You have been unassigned from your role")}"
                        : $"{(unitUpdate ? $" and unassigned as {AvsAn.Query(currentRole).Article} {currentRole}" : $"You have been unassigned as {AvsAn.Query(currentRole).Article} {currentRole}")}"
                );

                roleUpdate = true;
                positive = false;
            }

            return new(roleUpdate, positive);
        }

        private async Task<Tuple<bool, bool>> UpdateRank(string id, string rank, bool unitUpdate, bool roleUpdate, StringBuilder notificationMessage)
        {
            bool rankUpdate = false;
            bool positive = true;
            string currentRank = _accountContext.GetSingle(id).Rank;
            if (!string.IsNullOrEmpty(rank) && rank != REMOVE_FLAG)
            {
                if (currentRank == rank)
                {
                    return new(false, true);
                }

                await _accountContext.Update(id, x => x.Rank, rank);
                bool promotion = string.IsNullOrEmpty(currentRank) || _ranksService.IsSuperior(rank, currentRank);
                notificationMessage.Append(
                    $"{(unitUpdate || roleUpdate ? $" and {(promotion ? "promoted" : "demoted")} to {rank}" : $"You have been {(promotion ? "promoted" : "demoted")} to {rank}")}"
                );
                rankUpdate = true;
            }
            else if (rank == REMOVE_FLAG)
            {
                await _accountContext.Update(id, x => x.Rank, null);
                notificationMessage.Append($"{(unitUpdate || roleUpdate ? $" and demoted from {currentRank}" : $"You have been demoted from {currentRank}")}");
                rankUpdate = true;
                positive = false;
            }

            return new(rankUpdate, positive);
        }
    }
}
