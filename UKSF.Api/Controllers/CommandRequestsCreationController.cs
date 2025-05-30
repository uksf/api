using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("CommandRequests/Create")]
public class CommandRequestsCreationController(
    IAccountContext accountContext,
    IUnitsContext unitsContext,
    ICommandRequestService commandRequestService,
    IRanksService ranksService,
    ILoaService loaService,
    IUnitsService unitsService,
    IDisplayNameService displayNameService,
    IHttpContextService httpContextService
) : ControllerBase
{
    [HttpPut("rank")]
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

    [HttpPut("loa")]
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

    [HttpPut("discharge")]
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

    [HttpPut("role")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestIndividualRole([FromBody] DomainCommandRequest request)
    {
        request.Requester = httpContextService.GetUserId();
        request.DisplayValue = request.Value;
        request.DisplayFrom = accountContext.GetSingle(request.Recipient).RoleAssignment;
        request.Type = CommandRequestType.IndividualRole;
        if (commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await commandRequestService.Add(request, ChainOfCommandMode.Next_Commander);
    }

    [HttpPut("unitrole")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestUnitRole([FromBody] DomainCommandRequest request)
    {
        var unit = unitsContext.GetSingle(request.Value);
        var recipientHasChainOfCommandRole = unitsService.ChainOfCommandHasMember(unit, request.Recipient);

        if (!recipientHasChainOfCommandRole && request.SecondaryValue == "None")
        {
            throw new BadRequestException(
                $"{displayNameService.GetDisplayName(request.Recipient)} has no chain of command position in {unit.Name}. If you are trying to remove them from the unit, use a Unit Removal request"
            );
        }

        // Validate that member is in unit when assigning a chain of command position
        if (request.SecondaryValue != "None" && !unit.Members.Contains(request.Recipient))
        {
            throw new BadRequestException(
                $"{displayNameService.GetDisplayName(request.Recipient)} is not a member of {unit.Name}. They must be a unit member before being assigned to a chain of command position"
            );
        }

        // Validate that member is not already assigned to the requested position
        if (request.SecondaryValue != "None" && unitsService.MemberHasChainOfCommandPosition(request.Recipient, unit, request.SecondaryValue))
        {
            throw new BadRequestException(
                $"{displayNameService.GetDisplayName(request.Recipient)} is already assigned to {request.SecondaryValue} in {unit.Name}"
            );
        }

        request.Requester = httpContextService.GetUserId();
        request.DisplayValue = request.SecondaryValue == "None"
            ? $"Remove chain of command position from {unit.Name}"
            : $"{request.SecondaryValue} of {unit.Name}";
        if (recipientHasChainOfCommandRole)
        {
            var currentPosition = GetCurrentChainOfCommandPosition(unit, request.Recipient);
            request.DisplayFrom = $"{currentPosition} of {unit.Name}";
        }
        else
        {
            request.DisplayFrom = $"Member of {unit.Name}";
        }

        request.Type = CommandRequestType.UnitRole;
        if (commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await commandRequestService.Add(request);
    }

    private string GetCurrentChainOfCommandPosition(DomainUnit unit, string memberId)
    {
        if (unit.ChainOfCommand?.OneIC == memberId) return "1iC";
        if (unit.ChainOfCommand?.TwoIC == memberId) return "2iC";
        if (unit.ChainOfCommand?.ThreeIC == memberId) return "3iC";
        if (unit.ChainOfCommand?.NCOIC == memberId) return "NCOiC";
        return "Member";
    }

    [HttpPut("unitremoval")]
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

    [HttpPut("transfer")]
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

    [HttpPut("reinstate")]
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
}
