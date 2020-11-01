using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base;
using UKSF.Api.Base.Services;
using UKSF.Api.Interfaces.Command;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Command;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Controllers.CommandRequests {
    [Route("CommandRequests/Create")]
    public class CommandRequestsCreationController : Controller {
        private readonly IAccountService accountService;
        private readonly ICommandRequestService commandRequestService;
        private readonly IDisplayNameService displayNameService;
        private readonly IHttpContextService httpContextService;
        private readonly ILoaService loaService;
        private readonly IRanksService ranksService;

        private readonly string sessionId;
        private readonly IUnitsService unitsService;

        public CommandRequestsCreationController(IAccountService accountService, ICommandRequestService commandRequestService, IRanksService ranksService, ILoaService loaService, IUnitsService unitsService, IDisplayNameService displayNameService, IHttpContextService httpContextService) {
            this.accountService = accountService;
            this.commandRequestService = commandRequestService;
            this.ranksService = ranksService;
            this.loaService = loaService;
            this.unitsService = unitsService;
            this.displayNameService = displayNameService;
            this.httpContextService = httpContextService;
            sessionId = httpContextService.GetUserId();
        }

        [HttpPut("rank"), Authorize, Permissions(Permissions.COMMAND)]
        public async Task<IActionResult> CreateRequestRank([FromBody] CommandRequest request) {
            request.requester = sessionId;
            request.displayValue = request.value;
            request.displayFrom = accountService.Data.GetSingle(request.recipient).rank;
            if (request.displayValue == request.displayFrom) return BadRequest("Ranks are equal");
            bool direction = ranksService.IsSuperior(request.displayValue, request.displayFrom);
            request.type = string.IsNullOrEmpty(request.displayFrom) ? CommandRequestType.PROMOTION : direction ? CommandRequestType.PROMOTION : CommandRequestType.DEMOTION;
            if (commandRequestService.DoesEquivalentRequestExist(request)) return BadRequest("An equivalent request already exists");
            await commandRequestService.Add(request);
            return Ok();
        }

        [HttpPut("loa"), Authorize, Permissions(Permissions.MEMBER)]
        public async Task<IActionResult> CreateRequestLoa([FromBody] CommandRequestLoa request) {
            DateTime now = DateTime.UtcNow;
            if (request.start <= now.AddDays(-1)) {
                return BadRequest("Start date cannot be in the past");
            }

            if (request.end <= now) {
                return BadRequest("End date cannot be in the past");
            }

            if (request.end <= request.start) {
                return BadRequest("End date cannot be before start date");
            }

            request.recipient = sessionId;
            request.requester = sessionId;
            request.displayValue = request.end.ToString(CultureInfo.InvariantCulture);
            request.displayFrom = request.start.ToString(CultureInfo.InvariantCulture);
            request.type = CommandRequestType.LOA;
            if (commandRequestService.DoesEquivalentRequestExist(request)) return BadRequest("An equivalent request already exists");
            request.value = await loaService.Add(request);
            await commandRequestService.Add(request, ChainOfCommandMode.NEXT_COMMANDER_EXCLUDE_SELF);
            return Ok();
        }

        [HttpPut("discharge"), Authorize, Permissions(Permissions.COMMAND)]
        public async Task<IActionResult> CreateRequestDischarge([FromBody] CommandRequest request) {
            request.requester = sessionId;
            request.displayValue = "Discharged";
            request.displayFrom = "Member";
            request.type = CommandRequestType.DISCHARGE;
            if (commandRequestService.DoesEquivalentRequestExist(request)) return BadRequest("An equivalent request already exists");
            await commandRequestService.Add(request, ChainOfCommandMode.COMMANDER_AND_PERSONNEL);
            return Ok();
        }

        [HttpPut("role"), Authorize, Permissions(Permissions.COMMAND)]
        public async Task<IActionResult> CreateRequestIndividualRole([FromBody] CommandRequest request) {
            request.requester = sessionId;
            request.displayValue = request.value;
            request.displayFrom = accountService.Data.GetSingle(request.recipient).roleAssignment;
            request.type = CommandRequestType.INDIVIDUAL_ROLE;
            if (commandRequestService.DoesEquivalentRequestExist(request)) return BadRequest("An equivalent request already exists");
            await commandRequestService.Add(request, ChainOfCommandMode.NEXT_COMMANDER);
            return Ok();
        }

        [HttpPut("unitrole"), Authorize, Permissions(Permissions.COMMAND)]
        public async Task<IActionResult> CreateRequestUnitRole([FromBody] CommandRequest request) {
            Unit unit = unitsService.Data.GetSingle(request.value);
            bool recipientHasUnitRole = unitsService.RolesHasMember(unit, request.recipient);
            if (!recipientHasUnitRole && request.secondaryValue == "None") {
                return BadRequest($"{displayNameService.GetDisplayName(request.recipient)} has no unit role in {unit.name}. If you are trying to remove them from the unit, use a Unit Removal request");
            }

            request.requester = sessionId;
            request.displayValue = request.secondaryValue == "None" ? $"Remove role from {unit.name}" : $"{request.secondaryValue} of {unit.name}";
            if (recipientHasUnitRole) {
                string role = unit.roles.FirstOrDefault(x => x.Value == request.recipient).Key;
                request.displayFrom = $"{role} of {unit.name}";
            } else {
                request.displayFrom = $"Member of {unit.name}";
            }

            request.type = CommandRequestType.UNIT_ROLE;
            if (commandRequestService.DoesEquivalentRequestExist(request)) return BadRequest("An equivalent request already exists");
            await commandRequestService.Add(request);
            return Ok();
        }

        [HttpPut("unitremoval"), Authorize, Permissions(Permissions.COMMAND)]
        public async Task<IActionResult> CreateRequestUnitRemoval([FromBody] CommandRequest request) {
            Unit removeUnit = unitsService.Data.GetSingle(request.value);
            if (removeUnit.branch == UnitBranch.COMBAT) {
                return BadRequest("To remove from a combat unit, use a Transfer request");
            }

            request.requester = sessionId;
            request.displayValue = "N/A";
            request.displayFrom = removeUnit.name;
            request.type = CommandRequestType.UNIT_REMOVAL;
            if (commandRequestService.DoesEquivalentRequestExist(request)) return BadRequest("An equivalent request already exists");
            await commandRequestService.Add(request, ChainOfCommandMode.TARGET_COMMANDER);
            return Ok();
        }

        [HttpPut("transfer"), Authorize, Permissions(Permissions.COMMAND)]
        public async Task<IActionResult> CreateRequestTransfer([FromBody] CommandRequest request) {
            Unit toUnit = unitsService.Data.GetSingle(request.value);
            request.requester = sessionId;
            request.displayValue = toUnit.name;
            if (toUnit.branch == UnitBranch.AUXILIARY) {
                request.displayFrom = "N/A";
                request.type = CommandRequestType.AUXILIARY_TRANSFER;
                if (commandRequestService.DoesEquivalentRequestExist(request)) return BadRequest("An equivalent request already exists");
                await commandRequestService.Add(request, ChainOfCommandMode.TARGET_COMMANDER);
            } else {
                request.displayFrom = accountService.Data.GetSingle(request.recipient).unitAssignment;
                request.type = CommandRequestType.TRANSFER;
                if (commandRequestService.DoesEquivalentRequestExist(request)) return BadRequest("An equivalent request already exists");
                await commandRequestService.Add(request, ChainOfCommandMode.COMMANDER_AND_TARGET_COMMANDER);
            }

            return Ok();
        }

        [HttpPut("reinstate"), Authorize, Permissions(Permissions.COMMAND, Permissions.RECRUITER, Permissions.NCO)]
        public async Task<IActionResult> CreateRequestReinstateMember([FromBody] CommandRequest request) {
            request.requester = sessionId;
            request.displayValue = "Member";
            request.displayFrom = "Discharged";
            request.type = CommandRequestType.REINSTATE_MEMBER;
            if (commandRequestService.DoesEquivalentRequestExist(request)) return BadRequest("An equivalent request already exists");
            await commandRequestService.Add(request, ChainOfCommandMode.PERSONNEL);
            return Ok();
        }
    }
}
