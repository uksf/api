using System.Threading.Tasks;
using AvsAnLib;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Signalr.Clients;
using UKSF.Api.Command.Signalr.Hubs;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Command.Services
{
    public interface ICommandRequestCompletionService
    {
        Task Resolve(string id);
    }

    public class CommandRequestCompletionService : ICommandRequestCompletionService
    {
        private readonly IAccountContext _accountContext;
        private readonly IAssignmentService _assignmentService;
        private readonly ICommandRequestContext _commandRequestContext;
        private readonly ICommandRequestService _commandRequestService;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> _commandRequestsHub;
        private readonly IDischargeContext _dischargeContext;
        private readonly IHttpContextService _httpContextService;
        private readonly ILoaService _loaService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;
        private readonly IUnitsContext _unitsContext;

        private readonly IUnitsService _unitsService;

        public CommandRequestCompletionService(
            IDischargeContext dischargeContext,
            ICommandRequestContext commandRequestContext,
            IAccountContext accountContext,
            IUnitsContext unitsContext,
            IHttpContextService httpContextService,
            ICommandRequestService commandRequestService,
            IAssignmentService assignmentService,
            ILoaService loaService,
            IUnitsService unitsService,
            IHubContext<CommandRequestsHub, ICommandRequestsClient> commandRequestsHub,
            INotificationsService notificationsService,
            ILogger logger
        )
        {
            _dischargeContext = dischargeContext;
            _commandRequestContext = commandRequestContext;
            _accountContext = accountContext;
            _unitsContext = unitsContext;
            _httpContextService = httpContextService;
            _commandRequestService = commandRequestService;
            _assignmentService = assignmentService;
            _loaService = loaService;
            _unitsService = unitsService;
            _commandRequestsHub = commandRequestsHub;
            _notificationsService = notificationsService;
            _logger = logger;
        }

        public async Task Resolve(string id)
        {
            if (_commandRequestService.IsRequestApproved(id) || _commandRequestService.IsRequestRejected(id))
            {
                CommandRequest request = _commandRequestContext.GetSingle(id);
                switch (request.Type)
                {
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
                    default: throw new BadRequestException($"Request type not recognized: '{request.Type}'");
                }
            }

            await _commandRequestsHub.Clients.All.ReceiveRequestUpdate();
        }

        private async Task Rank(CommandRequest request)
        {
            if (_commandRequestService.IsRequestApproved(request.Id))
            {
                string role = HandleRecruitToPrivate(request.Recipient, request.Value);
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(request.Recipient, rankString: request.Value, role: role, reason: request.Reason);
                _notificationsService.Add(notification);
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            }
            else if (_commandRequestService.IsRequestRejected(request.Id))
            {
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task Loa(CommandRequest request)
        {
            if (_commandRequestService.IsRequestApproved(request.Id))
            {
                await _loaService.SetLoaState(request.Value, LoaReviewState.APPROVED);
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            }
            else if (_commandRequestService.IsRequestRejected(request.Id))
            {
                await _loaService.SetLoaState(request.Value, LoaReviewState.REJECTED);
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task Discharge(CommandRequest request)
        {
            if (_commandRequestService.IsRequestApproved(request.Id))
            {
                DomainAccount domainAccount = _accountContext.GetSingle(request.Recipient);
                Discharge discharge = new()
                {
                    Rank = domainAccount.Rank, Unit = domainAccount.UnitAssignment, Role = domainAccount.RoleAssignment, DischargedBy = request.DisplayRequester, Reason = request.Reason
                };
                DischargeCollection dischargeCollection = _dischargeContext.GetSingle(x => x.AccountId == domainAccount.Id);
                if (dischargeCollection == null)
                {
                    dischargeCollection = new() { AccountId = domainAccount.Id, Name = $"{domainAccount.Lastname}.{domainAccount.Firstname[0]}" };
                    dischargeCollection.Discharges.Add(discharge);
                    await _dischargeContext.Add(dischargeCollection);
                }
                else
                {
                    dischargeCollection.Discharges.Add(discharge);
                    await _dischargeContext.Update(
                        dischargeCollection.Id,
                        Builders<DischargeCollection>.Update.Set(x => x.Reinstated, false)
                                                     .Set(x => x.Name, $"{domainAccount.Lastname}.{domainAccount.Firstname[0]}")
                                                     .Set(x => x.Discharges, dischargeCollection.Discharges)
                    );
                }

                await _accountContext.Update(domainAccount.Id, x => x.MembershipState, MembershipState.DISCHARGED);

                Notification notification = await _assignmentService.UpdateUnitRankAndRole(
                    domainAccount.Id,
                    AssignmentService.REMOVE_FLAG,
                    AssignmentService.REMOVE_FLAG,
                    AssignmentService.REMOVE_FLAG,
                    request.Reason,
                    "",
                    AssignmentService.REMOVE_FLAG
                );
                _notificationsService.Add(notification);
                await _assignmentService.UnassignAllUnits(domainAccount.Id);
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            }
            else if (_commandRequestService.IsRequestRejected(request.Id))
            {
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task IndividualRole(CommandRequest request)
        {
            if (_commandRequestService.IsRequestApproved(request.Id))
            {
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(
                    request.Recipient,
                    role: request.Value == "None" ? AssignmentService.REMOVE_FLAG : request.Value,
                    reason: request.Reason
                );
                _notificationsService.Add(notification);
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            }
            else if (_commandRequestService.IsRequestRejected(request.Id))
            {
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task UnitRole(CommandRequest request)
        {
            if (_commandRequestService.IsRequestApproved(request.Id))
            {
                if (request.SecondaryValue == "None")
                {
                    if (string.IsNullOrEmpty(request.Value))
                    {
                        await _assignmentService.UnassignAllUnitRoles(request.Recipient);
                        _notificationsService.Add(new() { Owner = request.Recipient, Message = "You have been unassigned from all roles in all units", Icon = NotificationIcons.DEMOTION });
                    }
                    else
                    {
                        string role = await _assignmentService.UnassignUnitRole(request.Recipient, request.Value);
                        _notificationsService.Add(
                            new()
                            {
                                Owner = request.Recipient,
                                Message = $"You have been unassigned as {AvsAn.Query(role).Article} {role} in {_unitsService.GetChainString(_unitsContext.GetSingle(request.Value))}",
                                Icon = NotificationIcons.DEMOTION
                            }
                        );
                    }
                }
                else
                {
                    await _assignmentService.AssignUnitRole(request.Recipient, request.Value, request.SecondaryValue);
                    _notificationsService.Add(
                        new()
                        {
                            Owner = request.Recipient,
                            Message =
                                $"You have been assigned as {AvsAn.Query(request.SecondaryValue).Article} {request.SecondaryValue} in {_unitsService.GetChainString(_unitsContext.GetSingle(request.Value))}",
                            Icon = NotificationIcons.PROMOTION
                        }
                    );
                }

                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} as {request.DisplayValue} in {request.Value} because '{request.Reason}'");
            }
            else if (_commandRequestService.IsRequestRejected(request.Id))
            {
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} as {request.DisplayValue} in {request.Value}");
            }
        }

        private async Task UnitRemoval(CommandRequest request)
        {
            if (_commandRequestService.IsRequestApproved(request.Id))
            {
                Unit unit = _unitsContext.GetSingle(request.Value);
                await _assignmentService.UnassignUnit(request.Recipient, unit.Id);
                _notificationsService.Add(new() { Owner = request.Recipient, Message = $"You have been removed from {_unitsService.GetChainString(unit)}", Icon = NotificationIcons.DEMOTION });
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} because '{request.Reason}'");
            }
            else if (_commandRequestService.IsRequestRejected(request.Id))
            {
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom}");
            }
        }

        private async Task Transfer(CommandRequest request)
        {
            if (_commandRequestService.IsRequestApproved(request.Id))
            {
                Unit unit = _unitsContext.GetSingle(request.Value);
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(request.Recipient, unit.Name, reason: request.Reason);
                _notificationsService.Add(notification);
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            }
            else if (_commandRequestService.IsRequestRejected(request.Id))
            {
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private async Task Reinstate(CommandRequest request)
        {
            if (_commandRequestService.IsRequestApproved(request.Id))
            {
                DischargeCollection dischargeCollection = _dischargeContext.GetSingle(x => x.AccountId == request.Recipient);
                await _dischargeContext.Update(dischargeCollection.Id, x => x.Reinstated, true);
                await _accountContext.Update(dischargeCollection.AccountId, x => x.MembershipState, MembershipState.MEMBER);
                Notification notification = await _assignmentService.UpdateUnitRankAndRole(
                    dischargeCollection.AccountId,
                    "Basic Training Unit",
                    "Trainee",
                    "Recruit",
                    "",
                    "",
                    "your membership was reinstated"
                );
                _notificationsService.Add(notification);

                _logger.LogAudit($"{_httpContextService.GetUserId()} reinstated {dischargeCollection.Name}'s membership");
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            }
            else if (_commandRequestService.IsRequestRejected(request.Id))
            {
                await _commandRequestService.ArchiveRequest(request.Id);
                _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
            }
        }

        private string HandleRecruitToPrivate(string id, string targetRank)
        {
            DomainAccount domainAccount = _accountContext.GetSingle(id);
            return domainAccount.Rank == "Recruit" && targetRank == "Private" ? "Rifleman" : domainAccount.RoleAssignment;
        }
    }
}
