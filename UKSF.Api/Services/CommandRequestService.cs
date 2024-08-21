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

public class CommandRequestService : ICommandRequestService
{
    private readonly IAccountContext _accountContext;
    private readonly IAccountService _accountService;
    private readonly IChainOfCommandService _chainOfCommandService;
    private readonly ICommandRequestContext _commandRequestContext;
    private readonly ICommandRequestArchiveContext _dataArchive;
    private readonly IDisplayNameService _displayNameService;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;
    private readonly IRanksService _ranksService;
    private readonly IUnitsContext _unitsContext;

    public CommandRequestService(
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
    )
    {
        _accountContext = accountContext;
        _unitsContext = unitsContext;
        _commandRequestContext = commandRequestContext;
        _dataArchive = dataArchive;
        _notificationsService = notificationsService;
        _displayNameService = displayNameService;
        _accountService = accountService;
        _chainOfCommandService = chainOfCommandService;
        _ranksService = ranksService;
        _logger = logger;
    }

    public async Task Add(DomainCommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.Commander_And_One_Above)
    {
        var requesterDomainAccount = _accountService.GetUserAccount();
        var recipientDomainAccount = _accountContext.GetSingle(request.Recipient);
        request.DisplayRequester = _displayNameService.GetDisplayName(requesterDomainAccount);
        request.DisplayRecipient = _displayNameService.GetDisplayName(recipientDomainAccount);
        var ids = _chainOfCommandService.ResolveChain(
            mode,
            recipientDomainAccount.Id,
            _unitsContext.GetSingle(x => x.Name == recipientDomainAccount.UnitAssignment),
            _unitsContext.GetSingle(request.Value)
        );
        if (ids.Count == 0)
        {
            throw new Exception(
                $"Failed to get any commanders for review for {request.Type.ToLower()} request for {request.DisplayRecipient}.\nContact an admin"
            );
        }

        var accounts = ids.Select(x => _accountContext.GetSingle(x))
                          .OrderBy(x => x.Rank, new RankComparer(_ranksService))
                          .ThenBy(x => x.Lastname)
                          .ThenBy(x => x.Firstname)
                          .ToList();
        foreach (var account in accounts)
        {
            request.Reviews.Add(account.Id, ReviewState.Pending);
        }

        await _commandRequestContext.Add(request);
        _logger.LogAudit(
            $"{request.Type} request created for {request.DisplayRecipient} from {FormatIfDate(request.DisplayFrom)} to {FormatIfDate(request.DisplayValue)} because '{request.Reason}'"
        );

        var selfRequest = request.DisplayRequester == request.DisplayRecipient;
        var notificationMessage =
            $"{request.DisplayRequester} requires your review on {(selfRequest ? "their" : AvsAn.Query(request.Type).Article)} {request.Type.ToLower()} request{(selfRequest ? "" : $" for {request.DisplayRecipient}")}";
        foreach (var account in accounts.Where(x => x.Id != requesterDomainAccount.Id))
        {
            _notificationsService.Add(
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
        var request = _commandRequestContext.GetSingle(id);
        await _dataArchive.Add(request);
        await _commandRequestContext.Delete(id);
    }

    public async Task SetRequestReviewState(DomainCommandRequest request, string reviewerId, ReviewState newState)
    {
        await _commandRequestContext.Update(request.Id, Builders<DomainCommandRequest>.Update.Set($"reviews.{reviewerId}", newState));
    }

    public async Task SetRequestAllReviewStates(DomainCommandRequest request, ReviewState newState)
    {
        List<string> keys = [..request.Reviews.Keys];
        foreach (var key in keys)
        {
            request.Reviews[key] = newState;
        }

        await _commandRequestContext.Update(request.Id, Builders<DomainCommandRequest>.Update.Set(x => x.Reviews, request.Reviews));
    }

    public ReviewState GetReviewState(string id, string reviewer)
    {
        var request = _commandRequestContext.GetSingle(id);
        return request == null                     ? ReviewState.Error :
            !request.Reviews.ContainsKey(reviewer) ? ReviewState.Error : request.Reviews[reviewer];
    }

    public bool IsRequestApproved(string id)
    {
        return _commandRequestContext.GetSingle(id).Reviews.All(x => x.Value == ReviewState.Approved);
    }

    public bool IsRequestRejected(string id)
    {
        return _commandRequestContext.GetSingle(id).Reviews.Any(x => x.Value == ReviewState.Rejected);
    }

    public bool DoesEquivalentRequestExist(DomainCommandRequest request)
    {
        return DoesEquivalentRequestExist(
            request,
            x =>
                                              x.DisplayValue == request.DisplayValue &&
                                              x.DisplayFrom == request.DisplayFrom
                                     );
    }

    public bool DoesEquivalentRequestExist(DomainCommandRequest request, Func<DomainCommandRequest, bool> filter)
    {
        return _commandRequestContext.Get().Any(x => x.Recipient == request.Recipient && x.Type == request.Type && filter(x));
    }

    private static string FormatIfDate(string input)
    {
        return DateTime.TryParse(input, out var date) ? $"{date:dd MMM yyyy}" : input;
    }
}
