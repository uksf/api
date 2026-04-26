using System.Globalization;
using AvsAnLib;
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
    IUksfLogger logger,
    IAccountContext accountContext,
    IRanksService ranksService,
    ILoaService loaService,
    IChainOfCommandService chainOfCommandService
) : ControllerBase
{
    [HttpGet]
    [Permissions(Permissions.Command)]
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
            MyRequests = BuildCommandRequestItems(myRequests, contextId, canOverride, superAdmin, isOther: false, now),
            OtherRequests = BuildCommandRequestItems(otherRequests, contextId, canOverride, superAdmin, isOther: true, now)
        };
    }

    [HttpPatch("{id}")]
    [Permissions(Permissions.Command)]
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
            await commandRequestService.SetRequestOverride(request, state, sessionDomainAccount.Id);

            foreach (var reviewerId in request.Reviews.Select(x => x.Key).Where(x => x != sessionDomainAccount.Id))
            {
                notificationsService.Add(
                    new DomainNotification
                    {
                        Owner = reviewerId,
                        Icon = Icons.Request,
                        Message =
                            $"Your review on {AvsAn.Query(request.Type).Article} {request.Type.ToLower()} request for {request.DisplayRecipient} was overriden by {displayNameService.GetDisplayName(sessionDomainAccount)}"
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

            await commandRequestService.SetRequestReviewState(request, sessionDomainAccount.Id, state);
        }

        var willResolve = commandRequestService.IsRequestApproved(request.Id) || commandRequestService.IsRequestRejected(request.Id);

        if (!willResolve && !updateCommandReviewRequest.Overriden)
        {
            logger.LogAudit(
                $"Review state of {displayNameService.GetDisplayName(sessionDomainAccount)} for {request.Type.ToLower()} request for {request.DisplayRecipient} updated to {state}"
            );
        }

        try
        {
            await commandRequestCompletionService.Resolve(request.Id);
        }
        catch (Exception exception)
        {
            if (updateCommandReviewRequest.Overriden)
            {
                await commandRequestService.SetRequestOverride(request, ReviewState.Pending, null);
            }
            else
            {
                await commandRequestService.SetRequestReviewState(request, sessionDomainAccount.Id, ReviewState.Pending);
            }

            logger.LogError("Command request resolution failed", exception);
            throw new BadRequestException(exception.Message);
        }
    }

    [HttpPost("exists")]
    [Permissions(Permissions.Command)]
    public bool RequestExists([FromBody] DomainCommandRequest request)
    {
        return commandRequestService.DoesEquivalentRequestExist(request);
    }

    [HttpPost("Create/rank")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestRank([FromBody] DomainCommandRequest request)
    {
        request.Requester = httpContextService.GetUserId();
        request.DisplayValue = request.Value;
        request.DisplayFrom = accountContext.GetSingle(request.Recipient).Rank;
        if (request.DisplayValue == request.DisplayFrom)
        {
            throw new BadRequestException("Ranks are equal");
        }

        var direction = ranksService.IsSuperior(request.DisplayValue, request.DisplayFrom);
        request.Type = string.IsNullOrEmpty(request.DisplayFrom) ? CommandRequestType.Promotion :
            direction                                            ? CommandRequestType.Promotion : CommandRequestType.Demotion;
        if (commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await commandRequestService.Add(request);
    }

    [HttpPost("Create/loa")]
    [Permissions(Permissions.Member)]
    public async Task CreateRequestLoa([FromBody] CreateLoaRequest request)
    {
        var now = DateTime.UtcNow;
        if (request.Start <= now.AddDays(-1))
        {
            throw new BadRequestException("Start date cannot be in the past");
        }

        if (request.End <= now)
        {
            throw new BadRequestException("End date cannot be in the past");
        }

        if (request.End <= request.Start)
        {
            throw new BadRequestException("End date cannot be before start date");
        }

        var commandRequest = new DomainCommandRequest
        {
            Recipient = httpContextService.GetUserId(),
            Requester = httpContextService.GetUserId(),
            Reason = request.Reason,
            DisplayFrom = request.Start.ToString("O"),
            DisplayValue = request.End.ToString("O"),
            Type = CommandRequestType.Loa
        };
        if (commandRequestService.DoesEquivalentRequestExist(
                commandRequest,
                x =>
                {
                    var start = DateTime.Parse(x.DisplayFrom);
                    var end = DateTime.Parse(x.DisplayValue);
                    return request.Start >= start && request.End <= end;
                }
            ))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        commandRequest.Value = await loaService.Add(request, commandRequest.Recipient, commandRequest.Reason);
        await commandRequestService.Add(commandRequest, ChainOfCommandMode.Next_Commander_Exclude_Self);
    }

    [HttpPost("Create/discharge")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestDischarge([FromBody] DomainCommandRequest request)
    {
        request.Requester = httpContextService.GetUserId();
        request.DisplayValue = "Discharged";
        request.DisplayFrom = "Member";
        request.Type = CommandRequestType.Discharge;
        if (commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await commandRequestService.Add(request, ChainOfCommandMode.Commander_And_Personnel);
    }

    [HttpPost("Create/role")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestIndividualRole([FromBody] DomainCommandRequest request)
    {
        request.Requester = httpContextService.GetUserId();
        request.DisplayValue = request.Value;
        request.DisplayFrom = accountContext.GetSingle(request.Recipient).RoleAssignment;
        request.Type = CommandRequestType.Role;
        if (commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await commandRequestService.Add(request, ChainOfCommandMode.Next_Commander);
    }

    [HttpPost("Create/chainofcommandposition")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestChainOfCommandPosition([FromBody] DomainCommandRequest request)
    {
        var unit = unitsContext.GetSingle(request.Value);
        var recipientHasChainOfCommandRole = chainOfCommandService.ChainOfCommandHasMember(unit, request.Recipient);

        if (!recipientHasChainOfCommandRole && request.SecondaryValue == "None")
        {
            throw new BadRequestException(
                $"{displayNameService.GetDisplayName(request.Recipient)} has no chain of command position in {unit.Name}. If you are trying to remove them from the unit, use a Unit Removal request"
            );
        }

        if (request.SecondaryValue != "None" && !unit.Members.Contains(request.Recipient))
        {
            throw new BadRequestException(
                $"{displayNameService.GetDisplayName(request.Recipient)} is not a member of {unit.Name}. They must be a unit member before being assigned to a chain of command position"
            );
        }

        if (request.SecondaryValue != "None" && chainOfCommandService.MemberHasChainOfCommandPosition(request.Recipient, unit, request.SecondaryValue))
        {
            throw new BadRequestException(
                $"{displayNameService.GetDisplayName(request.Recipient)} is already assigned to {request.SecondaryValue} in {unit.Name}"
            );
        }

        request.Requester = httpContextService.GetUserId();
        request.DisplayValue = request.SecondaryValue == "None"
            ? $"Remove chain of command position from {unit.Shortname}"
            : $"{request.SecondaryValue} of {unit.Shortname}";
        if (recipientHasChainOfCommandRole)
        {
            var currentPosition = GetCurrentChainOfCommandPosition(unit, request.Recipient);
            request.DisplayFrom = $"{currentPosition} of {unit.Shortname}";
        }
        else
        {
            request.DisplayFrom = $"Member of {unit.Shortname}";
        }

        request.Type = CommandRequestType.ChainOfCommandPosition;
        if (commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await commandRequestService.Add(request);
    }

    [HttpPost("Create/unitremoval")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestUnitRemoval([FromBody] DomainCommandRequest request)
    {
        var removeUnit = unitsContext.GetSingle(request.Value);
        if (removeUnit.Branch == UnitBranch.Combat)
        {
            throw new BadRequestException("To remove from a combat unit, use a Transfer request");
        }

        request.Requester = httpContextService.GetUserId();
        request.DisplayValue = "N/A";
        request.DisplayFrom = removeUnit.Name;
        request.Type = CommandRequestType.UnitRemoval;
        if (commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await commandRequestService.Add(request, ChainOfCommandMode.Target_Commander);
    }

    [HttpPost("Create/transfer")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestTransfer([FromBody] DomainCommandRequest request)
    {
        var toUnit = unitsContext.GetSingle(request.Value);
        request.Requester = httpContextService.GetUserId();
        request.DisplayValue = toUnit.Name;
        if (toUnit.Branch == UnitBranch.Auxiliary)
        {
            request.DisplayFrom = "N/A";
            request.Type = CommandRequestType.AuxiliaryTransfer;
            if (commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await commandRequestService.Add(request, ChainOfCommandMode.Target_Commander);
        }
        else if (toUnit.Branch == UnitBranch.Secondary)
        {
            request.DisplayFrom = "N/A";
            request.Type = CommandRequestType.SecondaryTransfer;
            if (commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await commandRequestService.Add(request, ChainOfCommandMode.Target_Commander);
        }
        else
        {
            request.DisplayFrom = accountContext.GetSingle(request.Recipient).UnitAssignment;
            request.Type = CommandRequestType.Transfer;
            if (commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await commandRequestService.Add(request, ChainOfCommandMode.Commander_And_Target_Commander);
        }
    }

    [HttpPost("Create/reinstate")]
    [Permissions(Permissions.Command, Permissions.Recruiter, Permissions.Nco)]
    public async Task CreateRequestReinstateMember([FromBody] DomainCommandRequest request)
    {
        request.Requester = httpContextService.GetUserId();
        request.DisplayValue = "Member";
        request.DisplayFrom = "Discharged";
        request.Type = CommandRequestType.ReinstateMember;
        if (commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await commandRequestService.Add(request, ChainOfCommandMode.Personnel);
    }

    private string GetCurrentChainOfCommandPosition(DomainUnit unit, string memberId)
    {
        if (unit.ChainOfCommand?.First == memberId)
        {
            return "1iC";
        }

        if (unit.ChainOfCommand?.Second == memberId)
        {
            return "2iC";
        }

        if (unit.ChainOfCommand?.Third == memberId)
        {
            return "3iC";
        }

        if (unit.ChainOfCommand?.Nco == memberId)
        {
            return "NCOiC";
        }

        return "Member";
    }

    private IEnumerable<CommandRequestDataset> BuildCommandRequestItems(
        IEnumerable<DomainCommandRequest> requests,
        string contextId,
        bool canOverride,
        bool superAdmin,
        bool isOther,
        DateTime now
    )
    {
        return requests.Select(x => new CommandRequestDataset
            {
                Data = x,
                DisplayReason = string.IsNullOrEmpty(x.Reason) ? "None given" : x.Reason,
                DisplayType = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.Type.ToLower()),
                IconKey = ResolveIcon(x.Type),
                ColorKey = ResolveColorKey(x.Type),
                CanOverride = superAdmin ||
                              (canOverride &&
                               (isOther
                                   ? x.DateCreated.AddDays(1) < now
                                   : x.Reviews.Count > 1 &&
                                     x.DateCreated.AddDays(1) < now &&
                                     x.Reviews.Any(y => y.Value == ReviewState.Pending && y.Key != contextId))),
                Reviews = x.Reviews.Select(y => new CommandRequestReviewDataset
                    {
                        Id = y.Key,
                        Name = displayNameService.GetDisplayName(y.Key),
                        State = y.Value
                    }
                )
            }
        );
    }

    private static string ResolveIcon(string type) =>
        type switch
        {
            CommandRequestType.Promotion                                                                                => Icons.Promotion,
            CommandRequestType.Demotion                                                                                 => Icons.Demotion,
            CommandRequestType.Loa                                                                                      => Icons.Loa,
            CommandRequestType.Transfer or CommandRequestType.AuxiliaryTransfer or CommandRequestType.SecondaryTransfer => Icons.Transfer,
            CommandRequestType.Role                                                                                     => Icons.Role,
            CommandRequestType.ChainOfCommandPosition                                                                   => Icons.ChainOfCommandPosition,
            CommandRequestType.UnitRemoval                                                                              => Icons.UnitRemoval,
            CommandRequestType.Discharge                                                                                => Icons.Discharge,
            CommandRequestType.ReinstateMember                                                                          => Icons.Reinstate,
            _                                                                                                           => Icons.Request
        };

    private static string ResolveColorKey(string type) =>
        type switch
        {
            CommandRequestType.Promotion              => "promotion",
            CommandRequestType.Demotion               => "demotion",
            CommandRequestType.Loa                    => "loa",
            CommandRequestType.Transfer               => "transfer",
            CommandRequestType.AuxiliaryTransfer      => "aux-transfer",
            CommandRequestType.SecondaryTransfer      => "sec-transfer",
            CommandRequestType.Role                   => "role",
            CommandRequestType.ChainOfCommandPosition => "cocp",
            CommandRequestType.UnitRemoval            => "unit-removal",
            CommandRequestType.Discharge              => "discharge",
            CommandRequestType.ReinstateMember        => "reinstate",
            _                                         => "default"
        };
}
