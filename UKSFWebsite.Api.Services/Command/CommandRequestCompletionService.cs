using System;
using System.Threading.Tasks;
using AvsAnLib;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Command;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Command;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Models.Units;
using UKSFWebsite.Api.Services.Message;
using UKSFWebsite.Api.Services.Personnel;
using UKSFWebsite.Api.Signalr.Hubs.Command;

namespace UKSFWebsite.Api.Services.Command {
    public class CommandRequestCompletionService : ICommandRequestCompletionService {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly ICommandRequestService commandRequestService;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> commandRequestsHub;
        private readonly IDischargeService dischargeService;
        private readonly ILoaService loaService;
        private readonly INotificationsService notificationsService;
        private readonly ISessionService sessionService;
        private readonly IUnitsService unitsService;

        public CommandRequestCompletionService(
            ISessionService sessionService,
            IAccountService accountService,
            ICommandRequestService commandRequestService,
            IDischargeService dischargeService,
            IAssignmentService assignmentService,
            ILoaService loaService,
            IUnitsService unitsService,
            IHubContext<CommandRequestsHub, ICommandRequestsClient> commandRequestsHub,
            INotificationsService notificationsService
        ) {
            this.sessionService = sessionService;
            this.accountService = accountService;
            this.commandRequestService = commandRequestService;
            this.dischargeService = dischargeService;
            this.assignmentService = assignmentService;
            this.loaService = loaService;
            this.unitsService = unitsService;
            this.dischargeService = dischargeService;
            this.commandRequestsHub = commandRequestsHub;
            this.notificationsService = notificationsService;
        }

        public async Task Resolve(string id) {
            if (commandRequestService.IsRequestApproved(id) || commandRequestService.IsRequestRejected(id)) {
                CommandRequest request = commandRequestService.Data().GetSingle(id);
                switch (request.type) {
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
                    default: throw new InvalidOperationException($"Request type not recognized: '{request.type}'");
                }
            }

            await commandRequestsHub.Clients.All.ReceiveRequestUpdate();
        }

        private async Task Rank(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                Notification notification = await assignmentService.UpdateUnitRankAndRole(request.recipient, rankString: request.value, reason: request.reason);
                notificationsService.Add(notification);
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} from {request.displayFrom} to {request.displayValue} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} from {request.displayFrom} to {request.displayValue}");
            }
        }

        private async Task Loa(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                await loaService.SetLoaState(request.value, LoaReviewState.APPROVED);
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} from {request.displayFrom} to {request.displayValue} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await loaService.SetLoaState(request.value, LoaReviewState.REJECTED);
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} from {request.displayFrom} to {request.displayValue}");
            }
        }

        private async Task Discharge(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                Account account = accountService.Data().GetSingle(request.recipient);
                Discharge discharge = new Discharge {
                    rank = account.rank,
                    unit = account.unitAssignment,
                    role = account.roleAssignment,
                    dischargedBy = request.displayRequester,
                    reason = request.reason
                };
                DischargeCollection dischargeCollection = dischargeService.Data().GetSingle(x => x.accountId == account.id);
                if (dischargeCollection == null) {
                    dischargeCollection = new DischargeCollection {accountId = account.id, name = $"{account.lastname}.{account.firstname[0]}"};
                    dischargeCollection.discharges.Add(discharge);
                    await dischargeService.Data().Add(dischargeCollection);
                } else {
                    dischargeCollection.discharges.Add(discharge);
                    await dischargeService.Data().Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.reinstated, false));
                    await dischargeService.Data().Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.name, $"{account.lastname}.{account.firstname[0]}"));
                    await dischargeService.Data().Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.discharges, dischargeCollection.discharges));
                }

                await accountService.Data().Update(account.id, "membershipState", MembershipState.DISCHARGED);

                Notification notification = await assignmentService.UpdateUnitRankAndRole(account.id, AssignmentService.REMOVE_FLAG, AssignmentService.REMOVE_FLAG, AssignmentService.REMOVE_FLAG, request.reason, "", AssignmentService.REMOVE_FLAG);
                notificationsService.Add(notification);
                await assignmentService.UnassignAllUnits(account.id);
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} from {request.displayFrom} to {request.displayValue} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} from {request.displayFrom} to {request.displayValue}");
            }
        }

        private async Task IndividualRole(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                Notification notification = await assignmentService.UpdateUnitRankAndRole(request.recipient, role: request.value == "None" ? AssignmentService.REMOVE_FLAG : request.value, reason: request.reason);
                notificationsService.Add(notification);
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} from {request.displayFrom} to {request.displayValue} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} from {request.displayFrom} to {request.displayValue}");
            }
        }

        private async Task UnitRole(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                if (request.secondaryValue == "None") {
                    if (string.IsNullOrEmpty(request.value)) {
                        await assignmentService.UnassignAllUnitRoles(request.recipient);
                        notificationsService.Add(new Notification {owner = request.recipient, message = "You have been unassigned from all roles in all units", icon = NotificationIcons.DEMOTION});
                    } else {
                        string role = await assignmentService.UnassignUnitRole(request.recipient, request.value);
                        notificationsService.Add(new Notification {owner = request.recipient, message = $"You have been unassigned as {AvsAn.Query(role).Article} {role} in {unitsService.GetChainString(unitsService.Data().GetSingle(request.value))}", icon = NotificationIcons.DEMOTION});
                    }
                } else {
                    await assignmentService.AssignUnitRole(request.recipient, request.value, request.secondaryValue);
                    notificationsService.Add(
                        new Notification {owner = request.recipient, message = $"You have been assigned as {AvsAn.Query(request.secondaryValue).Article} {request.secondaryValue} in {unitsService.GetChainString(unitsService.Data().GetSingle(request.value))}", icon = NotificationIcons.PROMOTION}
                    );
                }

                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} as {request.displayValue} in {request.value} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} as {request.displayValue} in {request.value}");
            }
        }

        private async Task UnitRemoval(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                Unit unit = unitsService.Data().GetSingle(request.value);
                await assignmentService.UnassignUnit(request.recipient, unit.id);
                notificationsService.Add(new Notification {owner = request.recipient, message = $"You have been removed from {unitsService.GetChainString(unit)}", icon = NotificationIcons.DEMOTION});
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} from {request.displayFrom} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} from {request.displayFrom}");
            }
        }

        private async Task Transfer(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                Unit unit = unitsService.Data().GetSingle(request.value);
                Notification notification = await assignmentService.UpdateUnitRankAndRole(request.recipient, unit.name, reason: request.reason);
                notificationsService.Add(notification);
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} from {request.displayFrom} to {request.displayValue} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} from {request.displayFrom} to {request.displayValue}");
            }
        }

        private async Task Reinstate(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                DischargeCollection dischargeCollection = dischargeService.Data().GetSingle(x => x.accountId == request.recipient);
                await dischargeService.Data().Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.reinstated, true));
                await accountService.Data().Update(dischargeCollection.accountId, "membershipState", MembershipState.MEMBER);
                Notification notification = await assignmentService.UpdateUnitRankAndRole(dischargeCollection.accountId, "Basic Training Unit", "Trainee", "Recruit", "", "", "your membership was reinstated");
                notificationsService.Add(notification);

                LogWrapper.AuditLog(sessionService.GetContextId(), $"{sessionService.GetContextId()} reinstated {dischargeCollection.name}'s membership");
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} from {request.displayFrom} to {request.displayValue} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} from {request.displayFrom} to {request.displayValue}");
            }
        }
    }
}
