using AvsAnLib;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface ICommandRequestService
{
    Task Add(DomainCommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.Commander_And_One_Above);
    Task ArchiveRequest(string id);
    Task SetRequestReviewState(DomainCommandRequest request, string reviewerId, ReviewState newState);
    Task SetRequestAllReviewStates(DomainCommandRequest request, ReviewState newState);
    ReviewState GetReviewState(string id, string reviewer);
    bool IsRequestApproved(string id);
    bool IsRequestRejected(string id);
    bool DoesEquivalentRequestExist(DomainCommandRequest request);
    bool DoesEquivalentRequestExist(DomainCommandRequest request, Func<DomainCommandRequest, bool> filter);
}

public class CommandRequestService(
    IAccountContext accountContext,
    IUnitsContext unitsContext,
    ICommandRequestContext commandRequestContext,
    ICommandRequestArchiveContext dataArchive,
    INotificationsService notificationsService,
    IDisplayNameService displayNameService,
    IAccountService accountService,
    IChainOfCommandService chainOfCommandService,
    IRanksService ranksService,
    IUksfLogger logger
) : ICommandRequestService
{
    public async Task Add(DomainCommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.Commander_And_One_Above)
    {
        var requesterDomainAccount = accountService.GetUserAccount();
        var recipientDomainAccount = accountContext.GetSingle(request.Recipient);
        request.DisplayRequester = displayNameService.GetDisplayName(requesterDomainAccount);
        request.DisplayRecipient = displayNameService.GetDisplayName(recipientDomainAccount);
        var ids = chainOfCommandService.ResolveChain(
            mode,
            recipientDomainAccount.Id,
            unitsContext.GetSingle(x => x.Name == recipientDomainAccount.UnitAssignment),
            unitsContext.GetSingle(request.Value)
        );
        if (ids.Count == 0)
        {
            throw new Exception(
                $"Failed to get any commanders for review for {request.Type.ToLower()} request for {request.DisplayRecipient}.\nContact an admin"
            );
        }

        var accounts = ids.Select(accountContext.GetSingle)
                          .OrderBy(x => x.Rank, new RankComparer(ranksService))
                          .ThenBy(x => x.Lastname)
                          .ThenBy(x => x.Firstname)
                          .ToList();
        foreach (var account in accounts)
        {
            request.Reviews.Add(account.Id, ReviewState.Pending);
        }

        await commandRequestContext.Add(request);
        logger.LogAudit(
            $"{request.Type} request created for {request.DisplayRecipient} from {FormatIfDate(request.DisplayFrom)} to {FormatIfDate(request.DisplayValue)} because '{request.Reason}'"
        );

        var selfRequest = request.DisplayRequester == request.DisplayRecipient;
        var notificationMessage =
            $"{request.DisplayRequester} requires your review on {(selfRequest ? "their" : AvsAn.Query(request.Type).Article)} {request.Type.ToLower()} request{(selfRequest ? "" : $" for {request.DisplayRecipient}")}";
        foreach (var account in accounts.Where(x => x.Id != requesterDomainAccount.Id))
        {
            notificationsService.Add(
                new DomainNotification
                {
                    Owner = account.Id,
                    Icon = NotificationIcons.Request,
                    Message = notificationMessage,
                    Link = "/command/requests"
                }
            );
        }
    }

    public async Task ArchiveRequest(string id)
    {
        var request = commandRequestContext.GetSingle(id);
        await dataArchive.Add(request);
        await commandRequestContext.Delete(id);
    }

    public async Task SetRequestReviewState(DomainCommandRequest request, string reviewerId, ReviewState newState)
    {
        await commandRequestContext.Update(request.Id, Builders<DomainCommandRequest>.Update.Set($"reviews.{reviewerId}", newState));
    }

    public async Task SetRequestAllReviewStates(DomainCommandRequest request, ReviewState newState)
    {
        var updatedReviews = request.Reviews.ToDictionary(x => x.Key, _ => newState);
        await commandRequestContext.Update(request.Id, Builders<DomainCommandRequest>.Update.Set(x => x.Reviews, updatedReviews));
    }

    public ReviewState GetReviewState(string id, string reviewer)
    {
        var request = commandRequestContext.GetSingle(id);
        return request == null ? ReviewState.Error : request.Reviews.GetValueOrDefault(reviewer, ReviewState.Error);
    }

    public bool IsRequestApproved(string id)
    {
        return commandRequestContext.GetSingle(id).Reviews.All(x => x.Value == ReviewState.Approved);
    }

    public bool IsRequestRejected(string id)
    {
        return commandRequestContext.GetSingle(id).Reviews.Any(x => x.Value == ReviewState.Rejected);
    }

    public bool DoesEquivalentRequestExist(DomainCommandRequest request)
    {
        return DoesEquivalentRequestExist(request, x => x.DisplayValue == request.DisplayValue && x.DisplayFrom == request.DisplayFrom);
    }

    public bool DoesEquivalentRequestExist(DomainCommandRequest request, Func<DomainCommandRequest, bool> filter)
    {
        return commandRequestContext.Get().Any(x => x.Recipient == request.Recipient && x.Type == request.Type && filter(x));
    }

    private static string FormatIfDate(string input)
    {
        return DateTime.TryParse(input, out var date) ? $"{date:dd MMM yyyy}" : input;
    }
}
