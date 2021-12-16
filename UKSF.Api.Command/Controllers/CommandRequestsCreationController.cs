using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Command.Controllers
{
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

        [HttpPut("rank"), Permissions(Permissions.COMMAND)]
        public async Task CreateRequestRank([FromBody] CommandRequest request)
        {
            request.Requester = _httpContextService.GetUserId();
            request.DisplayValue = request.Value;
            request.DisplayFrom = _accountContext.GetSingle(request.Recipient).Rank;
            if (request.DisplayValue == request.DisplayFrom)
            {
                throw new BadRequestException("Ranks are equal");
            }

            var direction = _ranksService.IsSuperior(request.DisplayValue, request.DisplayFrom);
            request.Type = string.IsNullOrEmpty(request.DisplayFrom) ? CommandRequestType.PROMOTION :
                direction                                            ? CommandRequestType.PROMOTION : CommandRequestType.DEMOTION;
            if (_commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await _commandRequestService.Add(request);
        }

        [HttpPut("loa"), Permissions(Permissions.MEMBER)]
        public async Task CreateRequestLoa([FromBody] CommandRequestLoa request)
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
            request.DisplayValue = request.End.ToString("O");
            request.DisplayFrom = request.Start.ToString("O");
            request.Type = CommandRequestType.LOA;
            if (_commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            request.Value = await _loaService.Add(request);
            await _commandRequestService.Add(request, ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF);
        }

        [HttpPut("discharge"), Permissions(Permissions.COMMAND)]
        public async Task CreateRequestDischarge([FromBody] CommandRequest request)
        {
            request.Requester = _httpContextService.GetUserId();
            request.DisplayValue = "Discharged";
            request.DisplayFrom = "Member";
            request.Type = CommandRequestType.DISCHARGE;
            if (_commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await _commandRequestService.Add(request, ChainOfCommandMode.COMMANDER_AND_PERSONNEL);
        }

        [HttpPut("role"), Permissions(Permissions.COMMAND)]
        public async Task CreateRequestIndividualRole([FromBody] CommandRequest request)
        {
            request.Requester = _httpContextService.GetUserId();
            request.DisplayValue = request.Value;
            request.DisplayFrom = _accountContext.GetSingle(request.Recipient).RoleAssignment;
            request.Type = CommandRequestType.INDIVIDUAL_ROLE;
            if (_commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await _commandRequestService.Add(request, ChainOfCommandMode.NEXT_COMMANDER);
        }

        [HttpPut("unitrole"), Permissions(Permissions.COMMAND)]
        public async Task CreateRequestUnitRole([FromBody] CommandRequest request)
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

            request.Type = CommandRequestType.UNIT_ROLE;
            if (_commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await _commandRequestService.Add(request);
        }

        [HttpPut("unitremoval"), Permissions(Permissions.COMMAND)]
        public async Task CreateRequestUnitRemoval([FromBody] CommandRequest request)
        {
            var removeUnit = _unitsContext.GetSingle(request.Value);
            if (removeUnit.Branch == UnitBranch.COMBAT)
            {
                throw new BadRequestException("To remove from a combat unit, use a Transfer request");
            }

            request.Requester = _httpContextService.GetUserId();
            request.DisplayValue = "N/A";
            request.DisplayFrom = removeUnit.Name;
            request.Type = CommandRequestType.UNIT_REMOVAL;
            if (_commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await _commandRequestService.Add(request, ChainOfCommandMode.TARGET_COMMANDER);
        }

        [HttpPut("transfer"), Permissions(Permissions.COMMAND)]
        public async Task CreateRequestTransfer([FromBody] CommandRequest request)
        {
            var toUnit = _unitsContext.GetSingle(request.Value);
            request.Requester = _httpContextService.GetUserId();
            request.DisplayValue = toUnit.Name;
            if (toUnit.Branch == UnitBranch.AUXILIARY)
            {
                request.DisplayFrom = "N/A";
                request.Type = CommandRequestType.AUXILIARY_TRANSFER;
                if (_commandRequestService.DoesEquivalentRequestExist(request))
                {
                    throw new BadRequestException("An equivalent request already exists");
                }

                await _commandRequestService.Add(request, ChainOfCommandMode.TARGET_COMMANDER);
            }
            else
            {
                request.DisplayFrom = _accountContext.GetSingle(request.Recipient).UnitAssignment;
                request.Type = CommandRequestType.TRANSFER;
                if (_commandRequestService.DoesEquivalentRequestExist(request))
                {
                    throw new BadRequestException("An equivalent request already exists");
                }

                await _commandRequestService.Add(request, ChainOfCommandMode.COMMANDER_AND_TARGET_COMMANDER);
            }
        }

        [HttpPut("reinstate"), Permissions(Permissions.COMMAND, Permissions.RECRUITER, Permissions.NCO)]
        public async Task CreateRequestReinstateMember([FromBody] CommandRequest request)
        {
            request.Requester = _httpContextService.GetUserId();
            request.DisplayValue = "Member";
            request.DisplayFrom = "Discharged";
            request.Type = CommandRequestType.REINSTATE_MEMBER;
            if (_commandRequestService.DoesEquivalentRequestExist(request))
            {
                throw new BadRequestException("An equivalent request already exists");
            }

            await _commandRequestService.Add(request, ChainOfCommandMode.PERSONNEL);
        }
    }
}
