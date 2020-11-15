using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Command.Controllers {
    [Route("[controller]"), Permissions(Permissions.PERSONNEL, Permissions.NCO, Permissions.RECRUITER)]
    public class DischargesController : Controller {
        private readonly IAccountService _accountService;
        private readonly IAssignmentService _assignmentService;
        private readonly ICommandRequestService _commandRequestService;
        private readonly IDischargeService _dischargeService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;
        private readonly IUnitsService _unitsService;
        private readonly IVariablesDataService _variablesDataService;

        public DischargesController(
            IAccountService accountService,
            IAssignmentService assignmentService,
            ICommandRequestService commandRequestService,
            IDischargeService dischargeService,
            INotificationsService notificationsService,
            IHttpContextService httpContextService,
            IUnitsService unitsService,
            IVariablesDataService variablesDataService,
            ILogger logger
        ) {
            _accountService = accountService;
            _assignmentService = assignmentService;
            _commandRequestService = commandRequestService;
            _dischargeService = dischargeService;
            _notificationsService = notificationsService;
            _httpContextService = httpContextService;
            _unitsService = unitsService;
            _variablesDataService = variablesDataService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get() {
            IEnumerable<DischargeCollection> discharges = _dischargeService.Data.Get();
            foreach (DischargeCollection discharge in discharges) {
                discharge.requestExists = _commandRequestService.DoesEquivalentRequestExist(
                    new CommandRequest { Recipient = discharge.accountId, Type = CommandRequestType.REINSTATE_MEMBER, DisplayValue = "Member", DisplayFrom = "Discharged" }
                );
            }

            return Ok(discharges);
        }

        [HttpGet("reinstate/{id}")]
        public async Task<IActionResult> Reinstate(string id) {
            DischargeCollection dischargeCollection = _dischargeService.Data.GetSingle(id);
            await _dischargeService.Data.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.reinstated, true));
            await _accountService.Data.Update(dischargeCollection.accountId, "membershipState", MembershipState.MEMBER);
            Notification notification = await _assignmentService.UpdateUnitRankAndRole(
                dischargeCollection.accountId,
                "Basic Training Unit",
                "Trainee",
                "Recruit",
                "",
                "",
                "your membership was reinstated"
            );
            _notificationsService.Add(notification);

            _logger.LogAudit($"{_httpContextService.GetUserId()} reinstated {dischargeCollection.name}'s membership", _httpContextService.GetUserId());
            string personnelId = _variablesDataService.GetSingle("UNIT_ID_PERSONNEL").AsString();
            foreach (string member in _unitsService.Data.GetSingle(personnelId).members.Where(x => x != _httpContextService.GetUserId())) {
                _notificationsService.Add(
                    new Notification { owner = member, icon = NotificationIcons.PROMOTION, message = $"{dischargeCollection.name}'s membership was reinstated by {_httpContextService.GetUserId()}" }
                );
            }

            return Ok(_dischargeService.Data.Get());
        }
    }
}
