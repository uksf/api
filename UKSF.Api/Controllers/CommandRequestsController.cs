using System.Globalization;
using AvsAnLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Command)]
public class CommandRequestsController(
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
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public CommandRequestsDataset Get()
    {
        var allRequests = commandRequestContext.Get();
        List<DomainCommandRequest> myRequests = new();
        List<DomainCommandRequest> otherRequests = new();
        var contextId = httpContextService.GetUserId();
        var id = variablesContext.GetSingle("UNIT_ID_PERSONNEL").AsString();
        var canOverride = unitsContext.GetSingle(id).Members.Any(x => x == contextId);
        var superAdmin = httpContextService.UserHasPermission(Permissions.Superadmin);
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
        var sessionDomainAccount = accountService.GetUserAccount();
        var request = commandRequestContext.GetSingle(id);
        if (request == null)
        {
            throw new NotFoundException($"Request with id {id} not found");
        }

        var state = updateCommandReviewRequest.ReviewState;
        if (updateCommandReviewRequest.Overriden)
        {
            logger.LogAudit($"Review state of {request.Type.ToLower()} request for {request.DisplayRecipient} overriden to {state}");
            await commandRequestService.SetRequestAllReviewStates(request, state);

            foreach (var reviewerId in request.Reviews.Select(x => x.Key).Where(x => x != sessionDomainAccount.Id))
            {
                notificationsService.Add(
                    new DomainNotification
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
            var currentState = commandRequestService.GetReviewState(request.Id, sessionDomainAccount.Id);
            if (currentState == ReviewState.Error)
            {
                throw new BadRequestException(
                    $"Getting review state for {sessionDomainAccount} from {request.Id} failed. Reviews: \n{request.Reviews.Select(x => $"{x.Key}: {x.Value}").Aggregate((x, y) => $"{x}\n{y}")}"
                );
            }

            if (currentState == state)
            {
                return;
            }

            logger.LogAudit(
                $"Review state of {displayNameService.GetDisplayName(sessionDomainAccount)} for {request.Type.ToLower()} request for {request.DisplayRecipient} updated to {state}"
            );
            await commandRequestService.SetRequestReviewState(request, sessionDomainAccount.Id, state);
        }

        try
        {
            await commandRequestCompletionService.Resolve(request.Id);
        }
        catch (Exception exception)
        {
            if (updateCommandReviewRequest.Overriden)
            {
                await commandRequestService.SetRequestAllReviewStates(request, ReviewState.Pending);
            }
            else
            {
                await commandRequestService.SetRequestReviewState(request, sessionDomainAccount.Id, ReviewState.Pending);
            }

            throw new BadRequestException(exception.Message);
        }
    }

    [HttpPost("exists")]
    [Authorize]
    public bool RequestExists([FromBody] DomainCommandRequest request)
    {
        return commandRequestService.DoesEquivalentRequestExist(request);
    }

    private IEnumerable<CommandRequestDataset> GetMyRequests(
        IEnumerable<DomainCommandRequest> myRequests,
        string contextId,
        bool canOverride,
        bool superAdmin,
        DateTime now
    )
    {
        return myRequests.Select(x =>
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
                         x.Reviews.Any(y => y.Value == ReviewState.Pending && y.Key != contextId)),
                    Reviews = x.Reviews.Select(y => new CommandRequestReviewDataset
                        {
                            Id = y.Key,
                            Name = displayNameService.GetDisplayName(y.Key),
                            State = y.Value
                        }
                    )
                };
            }
        );
    }

    private IEnumerable<CommandRequestDataset> GetOtherRequests(
        IEnumerable<DomainCommandRequest> otherRequests,
        bool canOverride,
        bool superAdmin,
        DateTime now
    )
    {
        return otherRequests.Select(x =>
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
                    Reviews = x.Reviews.Select(y => new CommandRequestReviewDataset
                        {
                            Id = y.Key,
                            Name = displayNameService.GetDisplayName(y.Key),
                            State = y.Value
                        }
                    )
                };
            }
        );
    }
}
