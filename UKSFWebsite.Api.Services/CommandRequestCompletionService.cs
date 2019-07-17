using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Models.CommandRequests;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services {
    public class CommandRequestCompletionService : ICommandRequestCompletionService {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly ICommandRequestService commandRequestService;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> commandRequestsHub;
        private readonly IDischargeService dischargeService;
        private readonly ILoaService loaService;
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
            IHubContext<CommandRequestsHub, ICommandRequestsClient> commandRequestsHub
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
        }

        public async Task Resolve(string id) {
            if (commandRequestService.IsRequestApproved(id) || commandRequestService.IsRequestRejected(id)) {
                CommandRequest request = commandRequestService.GetSingle(id);
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
                await assignmentService.UpdateUnitRankAndRole(request.recipient, rankString: request.value, reason: request.reason);
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
                Account account = accountService.GetSingle(request.recipient);
                Discharge discharge = new Discharge {rank = account.rank, unit = account.unitAssignment, role = account.roleAssignment, dischargedBy = request.displayRequester, reason = request.reason};
                DischargeCollection dischargeCollection = dischargeService.GetSingle(x => x.accountId == account.id);
                if (dischargeCollection == null) {
                    dischargeCollection = new DischargeCollection {accountId = account.id, name = $"{account.lastname}.{account.firstname[0]}"};
                    dischargeCollection.discharges.Add(discharge);
                    await dischargeService.Add(dischargeCollection);
                } else {
                    dischargeCollection.discharges.Add(discharge);
                    await dischargeService.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.reinstated, false));
                    await dischargeService.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.name, $"{account.lastname}.{account.firstname[0]}"));
                    await dischargeService.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.discharges, dischargeCollection.discharges));
                }
                await accountService.Update(account.id, "membershipState", MembershipState.DISCHARGED);
                
                await assignmentService.UpdateUnitRankAndRole(account.id, AssignmentService.REMOVE_FLAG, AssignmentService.REMOVE_FLAG, AssignmentService.REMOVE_FLAG, request.reason, "", AssignmentService.REMOVE_FLAG);
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
                await assignmentService.UpdateUnitRankAndRole(request.recipient, role: request.value == "None" ? AssignmentService.REMOVE_FLAG : request.value, reason: request.reason);
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
                    } else {
                        await assignmentService.UnassignUnitRole(request.recipient, request.value);
                    }
                } else {
                    await assignmentService.AssignUnitRole(request.recipient, request.value, request.secondaryValue);
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
                Unit unit = unitsService.GetSingle(request.value);
                await assignmentService.UnassignUnit(request.recipient, unit.id);
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} from {request.displayFrom} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} from {request.displayFrom}");
            }
        }

        private async Task Transfer(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                Unit unit = unitsService.GetSingle(request.value);
                await assignmentService.UpdateUnitRankAndRole(request.recipient, unit.name, reason: request.reason);
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request approved for {request.displayRecipient} from {request.displayFrom} to {request.displayValue} because '{request.reason}'");
            } else if (commandRequestService.IsRequestRejected(request.id)) {
                await commandRequestService.ArchiveRequest(request.id);
                LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request rejected for {request.displayRecipient} from {request.displayFrom} to {request.displayValue}");
            }
        }

        private async Task Reinstate(CommandRequest request) {
            if (commandRequestService.IsRequestApproved(request.id)) {
                DischargeCollection dischargeCollection = dischargeService.GetSingle(x => x.accountId == request.recipient);
                await dischargeService.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.reinstated, true));
                await accountService.Update(dischargeCollection.accountId, "membershipState", MembershipState.MEMBER);
                await assignmentService.UpdateUnitRankAndRole(dischargeCollection.accountId, "Basic Training Unit", "Trainee", "Recruit", "", "", "your membership was reinstated");

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
