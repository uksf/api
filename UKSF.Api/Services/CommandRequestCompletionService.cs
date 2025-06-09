using AvsAnLib;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.Services;

public interface ICommandRequestCompletionService
{
    Task Resolve(string id);
}

public class CommandRequestCompletionService(
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
) : ICommandRequestCompletionService
{
    public async Task Resolve(string id)
    {
        if (commandRequestService.IsRequestApproved(id) || commandRequestService.IsRequestRejected(id))
        {
            var request = commandRequestContext.GetSingle(id);
            switch (request.Type)
            {
                case CommandRequestType.Promotion:
                case CommandRequestType.Demotion: await Rank(request); break;
                case CommandRequestType.Loa:                    await Loa(request); break;
                case CommandRequestType.Discharge:              await Discharge(request); break;
                case CommandRequestType.Role:                   await IndividualRole(request); break;
                case CommandRequestType.ChainOfCommandPosition: await ChainOfCommandPosition(request); break;
                case CommandRequestType.Transfer:
                case CommandRequestType.AuxiliaryTransfer:
                case CommandRequestType.SecondaryTransfer: await Transfer(request); break;
                case CommandRequestType.UnitRemoval:     await UnitRemoval(request); break;
                case CommandRequestType.ReinstateMember: await Reinstate(request); break;
                default:                                 throw new BadRequestException($"Request type not recognized: '{request.Type}'");
            }
        }

        await commandRequestsHub.Clients.All.ReceiveRequestUpdate();
    }

    private async Task Rank(DomainCommandRequest request)
    {
        if (commandRequestService.IsRequestApproved(request.Id))
        {
            var role = HandleRecruitToPrivate(request.Recipient, request.Value);
            var notification = await assignmentService.UpdateUnitRankAndRole(request.Recipient, rankString: request.Value, role: role, reason: request.Reason);
            notificationsService.Add(notification);
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
        }
        else if (commandRequestService.IsRequestRejected(request.Id))
        {
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
        }
    }

    private async Task Loa(DomainCommandRequest request)
    {
        if (commandRequestService.IsRequestApproved(request.Id))
        {
            await loaService.SetLoaState(request.Value, LoaReviewState.Approved);
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {FormatDate(request.DisplayFrom)} to {FormatDate(request.DisplayValue)} because '{request.Reason}'"
            );
        }
        else if (commandRequestService.IsRequestRejected(request.Id))
        {
            await loaService.SetLoaState(request.Value, LoaReviewState.Rejected);
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit(
                $"{request.Type} request rejected for {request.DisplayRecipient} from {FormatDate(request.DisplayFrom)} to {FormatDate(request.DisplayValue)}"
            );
        }
    }

    private async Task Discharge(DomainCommandRequest request)
    {
        if (commandRequestService.IsRequestApproved(request.Id))
        {
            var account = accountContext.GetSingle(request.Recipient);
            DomainDischarge discharge = new()
            {
                Rank = account.Rank,
                Unit = account.UnitAssignment,
                Role = account.RoleAssignment,
                DischargedBy = request.DisplayRequester,
                Reason = request.Reason
            };
            var dischargeCollection = dischargeContext.GetSingle(x => x.AccountId == account.Id);
            if (dischargeCollection == null)
            {
                dischargeCollection = new DomainDischargeCollection { AccountId = account.Id, Name = $"{account.Lastname}.{account.Firstname[0]}" };
                dischargeCollection.Discharges.Add(discharge);
                await dischargeContext.Add(dischargeCollection);
            }
            else
            {
                dischargeCollection.Discharges.Add(discharge);
                await dischargeContext.Update(
                    dischargeCollection.Id,
                    Builders<DomainDischargeCollection>.Update.Set(x => x.Reinstated, false)
                                                       .Set(x => x.Name, $"{account.Lastname}.{account.Firstname[0]}")
                                                       .Set(x => x.Discharges, dischargeCollection.Discharges)
                );
            }

            await accountContext.Update(account.Id, x => x.MembershipState, MembershipState.Discharged);

            var notification = await assignmentService.UpdateUnitRankAndRole(
                account.Id,
                AssignmentService.RemoveFlag,
                AssignmentService.RemoveFlag,
                AssignmentService.RemoveFlag,
                request.Reason,
                "",
                AssignmentService.RemoveFlag
            );
            notificationsService.Add(notification);
            await assignmentService.UnassignAllUnits(account.Id);
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
        }
        else if (commandRequestService.IsRequestRejected(request.Id))
        {
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
        }
    }

    private async Task IndividualRole(DomainCommandRequest request)
    {
        if (commandRequestService.IsRequestApproved(request.Id))
        {
            var notification = await assignmentService.UpdateUnitRankAndRole(
                request.Recipient,
                role: request.Value == "None" ? AssignmentService.RemoveFlag : request.Value,
                reason: request.Reason
            );
            notificationsService.Add(notification);
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
        }
        else if (commandRequestService.IsRequestRejected(request.Id))
        {
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
        }
    }

    private async Task ChainOfCommandPosition(DomainCommandRequest request)
    {
        if (commandRequestService.IsRequestApproved(request.Id))
        {
            if (request.SecondaryValue == "None")
            {
                if (string.IsNullOrEmpty(request.Value))
                {
                    await assignmentService.UnassignAllUnitChainOfCommandPositions(request.Recipient);
                    notificationsService.Add(
                        new DomainNotification
                        {
                            Owner = request.Recipient,
                            Message = "You have been unassigned from all chain of command positions in all units",
                            Icon = NotificationIcons.Demotion
                        }
                    );
                }
                else
                {
                    var position = await assignmentService.UnassignUnitChainOfCommandPosition(request.Recipient, request.Value);
                    notificationsService.Add(
                        new DomainNotification
                        {
                            Owner = request.Recipient,
                            Message =
                                $"You have been unassigned as {AvsAn.Query(position).Article} {position} in {unitsService.GetChainString(unitsContext.GetSingle(request.Value))}",
                            Icon = NotificationIcons.Demotion
                        }
                    );
                }
            }
            else
            {
                await assignmentService.AssignUnitChainOfCommandPosition(request.Recipient, request.Value, request.SecondaryValue);
                notificationsService.Add(
                    new DomainNotification
                    {
                        Owner = request.Recipient,
                        Message =
                            $"You have been assigned as {AvsAn.Query(request.SecondaryValue).Article} {request.SecondaryValue} in {unitsService.GetChainString(unitsContext.GetSingle(request.Value))}",
                        Icon = NotificationIcons.Promotion
                    }
                );
            }

            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} as {request.DisplayValue} in {request.Value} because '{request.Reason}'"
            );
        }
        else if (commandRequestService.IsRequestRejected(request.Id))
        {
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} as {request.DisplayValue} in {request.Value}");
        }
    }

    private async Task UnitRemoval(DomainCommandRequest request)
    {
        if (commandRequestService.IsRequestApproved(request.Id))
        {
            var unit = unitsContext.GetSingle(request.Value);
            await assignmentService.UnassignUnit(request.Recipient, unit.Id);
            notificationsService.Add(
                new DomainNotification
                {
                    Owner = request.Recipient,
                    Message = $"You have been removed from {unitsService.GetChainString(unit)}",
                    Icon = NotificationIcons.Demotion
                }
            );
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit($"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} because '{request.Reason}'");
        }
        else if (commandRequestService.IsRequestRejected(request.Id))
        {
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom}");
        }
    }

    private async Task Transfer(DomainCommandRequest request)
    {
        if (commandRequestService.IsRequestApproved(request.Id))
        {
            var unit = unitsContext.GetSingle(request.Value);
            var notification = await assignmentService.UpdateUnitRankAndRole(request.Recipient, unit.Name, reason: request.Reason);
            notificationsService.Add(notification);
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
        }
        else if (commandRequestService.IsRequestRejected(request.Id))
        {
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
        }
    }

    private async Task Reinstate(DomainCommandRequest request)
    {
        if (commandRequestService.IsRequestApproved(request.Id))
        {
            var dischargeCollection = dischargeContext.GetSingle(x => x.AccountId == request.Recipient);
            await dischargeContext.Update(dischargeCollection.Id, x => x.Reinstated, true);
            await accountContext.Update(dischargeCollection.AccountId, x => x.MembershipState, MembershipState.Member);
            var notification = await assignmentService.UpdateUnitRankAndRole(
                dischargeCollection.AccountId,
                "Basic Training Unit",
                "Trainee",
                "Recruit",
                "",
                "",
                "your membership was reinstated"
            );
            notificationsService.Add(notification);

            logger.LogAudit($"{httpContextService.GetUserId()} reinstated {dischargeCollection.Name}'s membership");
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit(
                $"{request.Type} request approved for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'"
            );
        }
        else if (commandRequestService.IsRequestRejected(request.Id))
        {
            await commandRequestService.ArchiveRequest(request.Id);
            logger.LogAudit($"{request.Type} request rejected for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue}");
        }
    }

    private string HandleRecruitToPrivate(string id, string targetRank)
    {
        var account = accountContext.GetSingle(id);
        return account.Rank == "Recruit" && targetRank == "Private" ? "Rifleman" : account.RoleAssignment;
    }

    private static string FormatDate(string input)
    {
        return DateTime.TryParse(input, out var date) ? $"{date:dd MMM yyyy}" : input;
    }
}
