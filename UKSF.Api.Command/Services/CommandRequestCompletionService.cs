using System;
using System.Threading.Tasks;
using AvsAnLib;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Signalr.Clients;
using UKSF.Api.Command.Signalr.Hubs;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Command.Services {
    public interface ICommandRequestCompletionService {
        Task Resolve(string id);
    }

    public class CommandRequestCompletionService : ICommandRequestCompletionService {
        private readonly IHttpContextService _httpContextService;
        private readonly IAccountService _accountService;
        private readonly IAssignmentService _assignmentService;
        private readonly ICommandRequestService _commandRequestService;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> _commandRequestsHub;
        private readonly IDischargeService _dischargeService;
        private readonly ILoaService _loaService;
        private readonly INotificationsService _notificationsService;
        private readonly ILogger _logger;

        private readonly IUnitsService _unitsService;

        public CommandRequestCompletionService(
            IHttpContextService httpContextService,
            IAccountService accountService,
            ICommandRequestService commandRequestService,
            IDischargeService dischargeService,
            IAssignmentService assignmentService,
            ILoaService loaService,
            IUnitsService unitsService,
            IHubContext<CommandRequestsHub, ICommandRequestsClient> commandRequestsHub,
            INotificationsService notificationsService,
            ILogger logger
        ) {
            _httpContextService = httpContextService;
            _accountService = accountService;
            _commandRequestService = commandRequestService;
            _dischargeService = dischargeService;
            _assignmentService = assignmentService;
            _loaService = loaService;
            _unitsService = unitsService;
            _dischargeService = dischargeService;
            _commandRequestsHub = commandRequestsHub;
            _notificationsService = notificationsService;
            _logger = logger;
        }

        public async Task Resolve(string id) {
            if (_commandRequestService.IsRequestApproved(id) || _commandRequestService.IsRequestRejected(id)) {
                CommandRequest request = _commandRequestService.Data.GetSingle(id);
                switch (request.Type) {
                    case CommandRequestType.PROMOTION:
                    case CommandRequestType.DEMOTION:
                        await Rank(request);
                        break;
                    case CommandRequestType.LOA:
                        await Loa(request);
                        break;
                    case CommandRequestType.DISCHARGE:
                        await Discharge(request);
                        break;
                    case CommandRequestType.INDIVIDUAL_ROLE:
                        await IndividualRole(request);
                        break;
                    case CommandRequestType.UNIT_ROLE:
                        await UnitRole(request);
                        break;
                    case CommandRequestType.TRANSFER:
                    case CommandRequestType.AUXILIARY_TRANSFER:
                        await Transfer(request);
                        break;
                    case CommandRequestType.UNIT_REMOVAL:
                        await UnitRemoval(request);
                        break;
                    case CommandRequestType.REINSTATE_MEMBER:
                        await Reinstate(request);
                        break;
                    default: throw new InvalidOperationException($"Request type not recognized: '{request.Type}'");
                }
            }

            await _commandRequestsHub.Clients.All.ReceiveRequestUpdate();
        }

        private async Task Rank(CommandRequest request) {
            if (_commandRequestService.IsRequestApproved(request.id)) {
                string role = HandleRecruitToPrivate(request.Recipient, request.Value);
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(request.Recipient, rankString: request.Value,  role: role, reason: request.Reason);
                _notificationsService.Add(notification);
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            } else if (_commandRequestService.IsRequestRejected(request.id)) {
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task Loa(CommandRequest request) {
            if (_commandRequestService.IsRequestApproved(request.id)) {
                await _loaService.SetLoaState(request.Value, LoaReviewState.APPROVED);
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            } else if (_commandRequestService.IsRequestRejected(request.id)) {
                await _loaService.SetLoaState(request.Value, LoaReviewState.REJECTED);
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task Discharge(CommandRequest request) {
            if (_commandRequestService.IsRequestApproved(request.id)) {
                Account account = _accountService.Data.GetSingle(request.Recipient);
                Discharge discharge = new Discharge {
                    rank = account.rank,
                    unit = account.unitAssignment,
                    role = account.roleAssignment,
                    dischargedBy = request.DisplayRequester,
                    reason = request.Reason
                };
                DischargeCollection dischargeCollection = _dischargeService.Data.GetSingle(x => x.accountId == account.id);
                if (dischargeCollection == null) {
                    dischargeCollection = new DischargeCollection {accountId = account.id, name = $"{account.lastname}.{account.firstname[0]}"};
                    dischargeCollection.discharges.Add(discharge);
                    await _dischargeService.Data.Add(dischargeCollection);
                } else {
                    dischargeCollection.discharges.Add(discharge);
                    await _dischargeService.Data.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.reinstated, false));
                    await _dischargeService.Data.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.name, $"{account.lastname}.{account.firstname[0]}"));
                    await _dischargeService.Data.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.discharges, dischargeCollection.discharges));
                }

                await _accountService.Data.Update(account.id, nameof(account.membershipState), MembershipState.DISCHARGED);

                Notification notification = await _assignmentService.UpdateUnitRankAndRole(account.id, AssignmentService.REMOVE_FLAG, AssignmentService.REMOVE_FLAG, AssignmentService.REMOVE_FLAG, request.Reason, "", AssignmentService.REMOVE_FLAG);
                _notificationsService.Add(notification);
                await _assignmentService.UnassignAllUnits(account.id);
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            } else if (_commandRequestService.IsRequestRejected(request.id)) {
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task IndividualRole(CommandRequest request) {
            if (_commandRequestService.IsRequestApproved(request.id)) {
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(request.Recipient, role: request.Value == "None" ? AssignmentService.REMOVE_FLAG : request.Value, reason: request.Reason);
                _notificationsService.Add(notification);
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            } else if (_commandRequestService.IsRequestRejected(request.id)) {
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task UnitRole(CommandRequest request) {
            if (_commandRequestService.IsRequestApproved(request.id)) {
                if (request.SecondaryValue == "None") {
                    if (string.IsNullOrEmpty(request.Value)) {
                        await _assignmentService.UnassignAllUnitRoles(request.Recipient);
                        _notificationsService.Add(new Notification {owner = request.Recipient, message = "You have been unassigned from all roles in all units", icon = NotificationIcons.DEMOTION});
                    } else {
                        string role = await _assignmentService.UnassignUnitRole(request.Recipient, request.Value);
                        _notificationsService.Add(new Notification {owner = request.Recipient, message = $"You have been unassigned as {AvsAn.Query(role).Article} {role} in {_unitsService.GetChainString(_unitsService.Data.GetSingle(request.Value))}", icon = NotificationIcons.DEMOTION});
                    }
                } else {
                    await _assignmentService.AssignUnitRole(request.Recipient, request.Value, request.SecondaryValue);
                    _notificationsService.Add(
                        new Notification {owner = request.Recipient, message = $"You have been assigned as {AvsAn.Query(request.SecondaryValue).Article} {request.SecondaryValue} in {_unitsService.GetChainString(_unitsService.Data.GetSingle(request.Value))}", icon = NotificationIcons.PROMOTION}
                    );
                }

                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} as {request.DisplayValue} in {request.Value} because '{request.Reason}'");
            } else if (_commandRequestService.IsRequestRejected(request.id)) {
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} as {request.DisplayValue} in {request.Value}");
            }
        }

        private async Task UnitRemoval(CommandRequest request) {
            if (_commandRequestService.IsRequestApproved(request.id)) {
                Unit unit = _unitsService.Data.GetSingle(request.Value);
                await _assignmentService.UnassignUnit(request.Recipient, unit.id);
                _notificationsService.Add(new Notification {owner = request.Recipient, message = $"You have been removed from {_unitsService.GetChainString(unit)}", icon = NotificationIcons.DEMOTION});
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} because '{request.Reason}'");
            } else if (_commandRequestService.IsRequestRejected(request.id)) {
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom}");
            }
        }

        private async Task Transfer(CommandRequest request) {
            if (_commandRequestService.IsRequestApproved(request.id)) {
                Unit unit = _unitsService.Data.GetSingle(request.Value);
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(request.Recipient, unit.name, reason: request.Reason);
                _notificationsService.Add(notification);
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            } else if (_commandRequestService.IsRequestRejected(request.id)) {
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task Reinstate(CommandRequest request) {
            if (_commandRequestService.IsRequestApproved(request.id)) {
                DischargeCollection dischargeCollection = _dischargeService.Data.GetSingle(x => x.accountId == request.Recipient);
                await _dischargeService.Data.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.reinstated, true));
                await _accountService.Data.Update(dischargeCollection.accountId, "membershipState", MembershipState.MEMBER);
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(dischargeCollection.accountId, "Basic Training Unit", "Trainee", "Recruit", "", "", "your membership was reinstated");
                _notificationsService.Add(notification);

                _logger.LogAudit($"{_httpContextService.GetUserId()} reinstated {dischargeCollection.name}'s membership");
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            } else if (_commandRequestService.IsRequestRejected(request.id)) {
                await _commandRequestService.ArchiveRequest(request.id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private string HandleRecruitToPrivate(string id, string targetRank) {
            Account account = _accountService.Data.GetSingle(id);
            return account.rank == "Recruit" && targetRank == "Private" ? "Rifleman" : account.roleAssignment;
        }
    }
}
