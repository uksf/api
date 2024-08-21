using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("CommandRequests/Create")]
public class CommandRequestsCreationController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly ICommandRequestService _commandRequestService;
    private readonly IDisplayNameService _displayNameService;
    private readonly IHttpContextService _httpContextService;
    private readonly ILoaService _loaService;
    private readonly IRanksService _ranksService;
    private readonly IUnitsContext _unitsContext;
    private readonly IUnitsService _unitsService;

    public CommandRequestsCreationController(
        IAccountContext accountContext,
        IUnitsContext unitsContext,
        ICommandRequestService commandRequestService,
        IRanksService ranksService,
        ILoaService loaService,
        IUnitsService unitsService,
        IDisplayNameService displayNameService,
        IHttpContextService httpContextService
    )
    {
        _accountContext = accountContext;
        _unitsContext = unitsContext;
        _commandRequestService = commandRequestService;
        _ranksService = ranksService;
        _loaService = loaService;
        _unitsService = unitsService;
        _displayNameService = displayNameService;
        _httpContextService = httpContextService;
    }

    [HttpPut("rank")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestRank([FromBody] DomainCommandRequest request)
    {
        request.Requester = _httpContextService.GetUserId();
        request.DisplayValue = request.Value;
        request.DisplayFrom = _accountContext.GetSingle(request.Recipient).Rank;
        if (request.DisplayValue == request.DisplayFrom)
        {
            throw new BadRequestException("Ranks are equal");
        }

        var direction = _ranksService.IsSuperior(request.DisplayValue, request.DisplayFrom);
        request.Type = string.IsNullOrEmpty(request.DisplayFrom) ? CommandRequestType.Promotion :
            direction                                            ? CommandRequestType.Promotion : CommandRequestType.Demotion;
        if (_commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await _commandRequestService.Add(request);
    }

    [HttpPut("loa")]
    [Permissions(Permissions.Member)]
    public async Task CreateRequestLoa([FromBody] DomainCommandRequestLoa request)
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

        request.Recipient = _httpContextService.GetUserId();
        request.Requester = _httpContextService.GetUserId();
        request.DisplayFrom = request.Start.ToString("O");
        request.DisplayValue = request.End.ToString("O");
        request.Type = CommandRequestType.Loa;
        if (_commandRequestService.DoesEquivalentRequestExist(
                request,
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

        request.Value = await _loaService.Add(request);
        await _commandRequestService.Add(request, ChainOfCommandMode.Next_Commander_Exclude_Self);
    }

    [HttpPut("discharge")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestDischarge([FromBody] DomainCommandRequest request)
    {
        request.Requester = _httpContextService.GetUserId();
        request.DisplayValue = "Discharged";
        request.DisplayFrom = "Member";
        request.Type = CommandRequestType.Discharge;
        if (_commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await _commandRequestService.Add(request, ChainOfCommandMode.Commander_And_Personnel);
    }

    [HttpPut("role")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestIndividualRole([FromBody] DomainCommandRequest request)
    {
        request.Requester = _httpContextService.GetUserId();
        request.DisplayValue = request.Value;
        request.DisplayFrom = _accountContext.GetSingle(request.Recipient).RoleAssignment;
        request.Type = CommandRequestType.IndividualRole;
        if (_commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await _commandRequestService.Add(request, ChainOfCommandMode.Next_Commander);
    }

    [HttpPut("unitrole")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestUnitRole([FromBody] DomainCommandRequest request)
    {
        var unit = _unitsContext.GetSingle(request.Value);
        var recipientHasUnitRole = _unitsService.RolesHasMember(unit, request.Recipient);
        if (!recipientHasUnitRole && request.SecondaryValue == "None")
        {
            throw new BadRequestException(
                $"{_displayNameService.GetDisplayName(request.Recipient)} has no unit role in {unit.Name}. If you are trying to remove them from the unit, use a Unit Removal request"
            );
        }

        request.Requester = _httpContextService.GetUserId();
        request.DisplayValue = request.SecondaryValue == "None" ? $"Remove role from {unit.Name}" : $"{request.SecondaryValue} of {unit.Name}";
        if (recipientHasUnitRole)
        {
            var role = unit.Roles.FirstOrDefault(x => x.Value == request.Recipient).Key;
            request.DisplayFrom = $"{role} of {unit.Name}";
        }
        else
        {
            request.DisplayFrom = $"Member of {unit.Name}";
        }

        request.Type = CommandRequestType.UnitRole;
        if (_commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await _commandRequestService.Add(request);
    }

    [HttpPut("unitremoval")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestUnitRemoval([FromBody] DomainCommandRequest request)
    {
        var removeUnit = _unitsContext.GetSingle(request.Value);
        if (removeUnit.Branch == UnitBranch.Combat)
        {
            throw new BadRequestException("To remove from a combat unit, use a Transfer request");
        }

        request.Requester = _httpContextService.GetUserId();
        request.DisplayValue = "N/A";
        request.DisplayFrom = removeUnit.Name;
        request.Type = CommandRequestType.UnitRemoval;
        if (_commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await _commandRequestService.Add(request, ChainOfCommandMode.Target_Commander);
    }

    [HttpPut("transfer")]
    [Permissions(Permissions.Command)]
    public async Task CreateRequestTransfer([FromBody] DomainCommandRequest request)
    {
        var toUnit = _unitsContext.GetSingle(request.Value);
        request.Requester = _httpContextService.GetUserId();
        request.DisplayValue = toUnit.Name;
        if (toUnit.Branch == UnitBranch.Auxiliary)
        {
            request.DisplayFrom = "N/A";
            request.Type = CommandRequestType.AuxiliaryTransfer;
            if (_commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await _commandRequestService.Add(request, ChainOfCommandMode.Target_Commander);
        }
        else
        {
            request.DisplayFrom = _accountContext.GetSingle(request.Recipient).UnitAssignment;
            request.Type = CommandRequestType.Transfer;
            if (_commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await _commandRequestService.Add(request, ChainOfCommandMode.Commander_And_Target_Commander);
        }
    }

    [HttpPut("reinstate")]
    [Permissions(Permissions.Command, Permissions.Recruiter, Permissions.Nco)]
    public async Task CreateRequestReinstateMember([FromBody] DomainCommandRequest request)
    {
        request.Requester = _httpContextService.GetUserId();
        request.DisplayValue = "Member";
        request.DisplayFrom = "Discharged";
        request.Type = CommandRequestType.ReinstateMember;
        if (_commandRequestService.DoesEquivalentRequestExist(request))
        {
            throw new BadRequestException("An equivalent request already exists");
        }

        await _commandRequestService.Add(request, ChainOfCommandMode.Personnel);
    }
}

