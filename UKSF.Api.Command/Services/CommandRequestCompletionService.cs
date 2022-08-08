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

namespace UKSF.Api.Command.Services;

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
    private readonly IUksfLogger _logger;
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
        IUksfLogger logger
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
            var request = _commandRequestContext.GetSingle(id);
            switch (request.Type)
            {
                case CommandRequestType.Promotion:
                case CommandRequestType.Demotion:
                    await Rank(request);
                    break;
                case CommandRequestType.Loa:
                    await Loa(request);
                    break;
                case CommandRequestType.Discharge:
                    await Discharge(request);
                    break;
                case CommandRequestType.IndividualRole:
                    await IndividualRole(request);
                    break;
                case CommandRequestType.UnitRole:
                    await UnitRole(request);
                    break;
                case CommandRequestType.Transfer:
                case CommandRequestType.AuxiliaryTransfer:
                    await Transfer(request);
                    break;
                case CommandRequestType.UnitRemoval:
                    await UnitRemoval(request);
                    break;
                case CommandRequestType.ReinstateMember:
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
            var role = HandleRecruitToPrivate(request.Recipient, request.Value);
            var notification = await _assignmentService.UpdateUnitRankAndRole(request.Recipient, rankString: request.Value, role: role, reason: request.Reason);
            _notificationsService.Add(notification);
            await _commandRequestService.ArchiveRequest(request.Id);
            _logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
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
            _logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {FormatDate(request.DisplayFrom)} to {FormatDate(request.DisplayValue)} because '{request.Reason}'"
            );
        }
        else if (_commandRequestService.IsRequestRejected(request.Id))
        {
            await _loaService.SetLoaState(request.Value, LoaReviewState.REJECTED);
            await _commandRequestService.ArchiveRequest(request.Id);
            _logger.LogAudit(
                $"{request.Type} request rejected for {request.DisplayRecipient} from {FormatDate(request.DisplayFrom)} to {FormatDate(request.DisplayValue)}"
            );
        }
    }

    private async Task Discharge(CommandRequest request)
    {
        if (_commandRequestService.IsRequestApproved(request.Id))
        {
            var domainAccount = _accountContext.GetSingle(request.Recipient);
            Discharge discharge = new()
            {
                Rank = domainAccount.Rank,
                Unit = domainAccount.UnitAssignment,
                Role = domainAccount.RoleAssignment,
                DischargedBy = request.DisplayRequester,
                Reason = request.Reason
            };
            var dischargeCollection = _dischargeContext.GetSingle(x => x.AccountId == domainAccount.Id);
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

            var notification = await _assignmentService.UpdateUnitRankAndRole(
                domainAccount.Id,
                AssignmentService.RemoveFlag,
                AssignmentService.RemoveFlag,
                AssignmentService.RemoveFlag,
                request.Reason,
                "",
                AssignmentService.RemoveFlag
            );
            _notificationsService.Add(notification);
            await _assignmentService.UnassignAllUnits(domainAccount.Id);
            await _commandRequestService.ArchiveRequest(request.Id);
            _logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
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
            var notification = await _assignmentService.UpdateUnitRankAndRole(
                request.Recipient,
                role: request.Value == "None" ? AssignmentService.RemoveFlag : request.Value,
                reason: request.Reason
            );
            _notificationsService.Add(notification);
            await _commandRequestService.ArchiveRequest(request.Id);
            _logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
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
                    _notificationsService.Add(
                        new() { Owner = request.Recipient, Message = "You have been unassigned from all roles in all units", Icon = NotificationIcons.Demotion }
                    );
                }
                else
                {
                    var role = await _assignmentService.UnassignUnitRole(request.Recipient, request.Value);
                    _notificationsService.Add(
                        new()
                        {
                            Owner = request.Recipient,
                            Message =
                                $"You have been unassigned as {AvsAn.Query(role).Article} {role} in {_unitsService.GetChainString(_unitsContext.GetSingle(request.Value))}",
                            Icon = NotificationIcons.Demotion
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
                        Icon = NotificationIcons.Promotion
                    }
                );
            }

            await _commandRequestService.ArchiveRequest(request.Id);
            _logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} as {request.DisplayValue} in {request.Value} because '{request.Reason}'"
            );
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
            var unit = _unitsContext.GetSingle(request.Value);
            await _assignmentService.UnassignUnit(request.Recipient, unit.Id);
            _notificationsService.Add(
                new()
                {
                    Owner = request.Recipient,
                    Message = $"You have been removed from {_unitsService.GetChainString(unit)}",
                    Icon = NotificationIcons.Demotion
                }
            );
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
            var unit = _unitsContext.GetSingle(request.Value);
            var notification = await _assignmentService.UpdateUnitRankAndRole(request.Recipient, unit.Name, reason: request.Reason);
            _notificationsService.Add(notification);
            await _commandRequestService.ArchiveRequest(request.Id);
            _logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
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
            var dischargeCollection = _dischargeContext.GetSingle(x => x.AccountId == request.Recipient);
            await _dischargeContext.Update(dischargeCollection.Id, x => x.Reinstated, true);
            await _accountContext.Update(dischargeCollection.AccountId, x => x.MembershipState, MembershipState.MEMBER);
            var notification = await _assignmentService.UpdateUnitRankAndRole(
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
            _logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
        }
        else if (_commandRequestService.IsRequestRejected(request.Id))
        {
            await _commandRequestService.ArchiveRequest(request.Id);
            _logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
        }
    }

    private string HandleRecruitToPrivate(string id, string targetRank)
    {
        var domainAccount = _accountContext.GetSingle(id);
        return domainAccount.Rank == "Recruit" && targetRank == "Private" ? "Rifleman" : domainAccount.RoleAssignment;
    }

    private static string FormatDate(string input)
    {
        return DateTime.TryParse(input, out var date) ? $"{date:dd MMM yyyy}" : input;
    }
}
