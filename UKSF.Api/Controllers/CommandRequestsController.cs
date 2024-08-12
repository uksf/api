using System.Globalization;
using AvsAnLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Command)]
public class CommandRequestsController : ControllerBase
{
    private const string SuperAdmin = "59e38f10594c603b78aa9dbd";
    private readonly IAccountService _accountService;
    private readonly ICommandRequestCompletionService _commandRequestCompletionService;
    private readonly ICommandRequestContext _commandRequestContext;
    private readonly ICommandRequestService _commandRequestService;
    private readonly IDisplayNameService _displayNameService;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;
    private readonly IUnitsContext _unitsContext;
    private readonly IVariablesContext _variablesContext;

    public CommandRequestsController(
        ICommandRequestService commandRequestService,
        ICommandRequestCompletionService commandRequestCompletionService,
        IHttpContextService httpContextService,
        IUnitsContext unitsContext,
        ICommandRequestContext commandRequestContext,
        IDisplayNameService displayNameService,
        INotificationsService notificationsService,
        IVariablesContext variablesContext,
        IAccountService accountService,
        IUksfLogger logger
    )
    {
        _commandRequestService = commandRequestService;
        _commandRequestCompletionService = commandRequestCompletionService;
        _httpContextService = httpContextService;
        _unitsContext = unitsContext;
        _commandRequestContext = commandRequestContext;
        _displayNameService = displayNameService;
        _notificationsService = notificationsService;
        _variablesContext = variablesContext;
        _accountService = accountService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public CommandRequestsDataset Get()
    {
        var allRequests = _commandRequestContext.Get();
        List<CommandRequest> myRequests = new();
        List<CommandRequest> otherRequests = new();
        var contextId = _httpContextService.GetUserId();
        var id = _variablesContext.GetSingle("UNIT_ID_PERSONNEL").AsString();
        var canOverride = _unitsContext.GetSingle(id).Members.Any(x => x == contextId);
        var superAdmin = contextId == SuperAdmin;
        var now = DateTime.UtcNow;
        foreach (var commandRequest in allRequests)
        {
            var reviewers = commandRequest.Reviews.Keys;
            if (reviewers.Any(x => x == contextId))
            {
                myRequests.Add(commandRequest);
            }
            else
            {
                otherRequests.Add(commandRequest);
            }
        }

        return new CommandRequestsDataset
        {
            MyRequests = GetMyRequests(myRequests, contextId, canOverride, superAdmin, now),
            OtherRequests = GetOtherRequests(otherRequests, canOverride, superAdmin, now)
        };
    }

    [HttpPatch("{id}")]
    [Authorize]
    public async Task UpdateRequestReview([FromRoute] string id, [FromBody] UpdateCommandReviewRequest updateCommandReviewRequest)
    {
        var sessionDomainAccount = _accountService.GetUserAccount();
        var request = _commandRequestContext.GetSingle(id);
        if (request == null)
        {
            throw new NotFoundException($"Request with id {id} not found");
        }

        var state = updateCommandReviewRequest.ReviewState;
        if (updateCommandReviewRequest.Overriden)
        {
            _logger.LogAudit($"Review state of {request.Type.ToLower()} request for {request.DisplayRecipient} overriden to {state}");
            await _commandRequestService.SetRequestAllReviewStates(request, state);

            foreach (var reviewerId in request.Reviews.Select(x => x.Key).Where(x => x != sessionDomainAccount.Id))
            {
                _notificationsService.Add(
                    new Notification
                    {
                        Owner = reviewerId,
                        Icon = NotificationIcons.Request,
                        Message =
                            $"Your review on {AvsAn.Query(request.Type).Article} {request.Type.ToLower()} request for {request.DisplayRecipient} was overriden by {sessionDomainAccount.Id}"
                    }
                );
            }
        }
        else
        {
            var currentState = _commandRequestService.GetReviewState(request.Id, sessionDomainAccount.Id);
            if (currentState == ReviewState.ERROR)
            {
                throw new BadRequestException(
                    $"Getting review state for {sessionDomainAccount} from {request.Id} failed. Reviews: \n{request.Reviews.Select(x => $"{x.Key}: {x.Value}").Aggregate((x, y) => $"{x}\n{y}")}"
                );
            }

            if (currentState == state)
            {
                return;
            }

            _logger.LogAudit(
                $"Review state of {_displayNameService.GetDisplayName(sessionDomainAccount)} for {request.Type.ToLower()} request for {request.DisplayRecipient} updated to {state}"
            );
            await _commandRequestService.SetRequestReviewState(request, sessionDomainAccount.Id, state);
        }

        try
        {
            await _commandRequestCompletionService.Resolve(request.Id);
        }
        catch (Exception exception)
        {
            if (updateCommandReviewRequest.Overriden)
            {
                await _commandRequestService.SetRequestAllReviewStates(request, ReviewState.PENDING);
            }
            else
            {
                await _commandRequestService.SetRequestReviewState(request, sessionDomainAccount.Id, ReviewState.PENDING);
            }

            throw new BadRequestException(exception.Message);
        }
    }

    [HttpPost("exists")]
    [Authorize]
    public bool RequestExists([FromBody] CommandRequest request)
    {
        return _commandRequestService.DoesEquivalentRequestExist(request);
    }

    private IEnumerable<CommandRequestDataset> GetMyRequests(
        IEnumerable<CommandRequest> myRequests,
        string contextId,
        bool canOverride,
        bool superAdmin,
        DateTime now
    )
    {
        return myRequests.Select(
            x =>
            {
                if (string.IsNullOrEmpty(x.Reason))
                {
                    x.Reason = "None given";
                }

                x.Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.Type.ToLower());
                return new CommandRequestDataset
                {
                    Data = x,
                    CanOverride =
                        superAdmin ||
                        (canOverride &&
                         x.Reviews.Count > 1 &&
                         x.DateCreated.AddDays(1) < now &&
                         x.Reviews.Any(y => y.Value == ReviewState.PENDING && y.Key != contextId)),
                    Reviews = x.Reviews.Select(
                        y => new CommandRequestReviewDataset { Id = y.Key, Name = _displayNameService.GetDisplayName(y.Key), State = y.Value }
                    )
                };
            }
        );
    }

    private IEnumerable<CommandRequestDataset> GetOtherRequests(IEnumerable<CommandRequest> otherRequests, bool canOverride, bool superAdmin, DateTime now)
    {
        return otherRequests.Select(
            x =>
            {
                if (string.IsNullOrEmpty(x.Reason))
                {
                    x.Reason = "None given";
                }

                x.Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.Type.ToLower());
                return new CommandRequestDataset
                {
                    Data = x,
                    CanOverride = superAdmin || (canOverride && x.DateCreated.AddDays(1) < now),
                    Reviews = x.Reviews.Select(
                        y => new CommandRequestReviewDataset { Id = y.Key, Name = _displayNameService.GetDisplayName(y.Key), State = y.Value }
                    )
                };
            }
        );
    }
}
