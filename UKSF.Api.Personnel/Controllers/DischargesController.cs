using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Admin.Services.Data;
using UKSF.Api.Base;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]"), Permissions(Permissions.PERSONNEL, Permissions.NCO, Permissions.RECRUITER)]
    public class DischargesController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly ICommandRequestService commandRequestService;
        private readonly IDischargeService dischargeService;
        private readonly INotificationsService notificationsService;
        private readonly IHttpContextService httpContextService;

        private readonly IUnitsService unitsService;
        private readonly IVariablesDataService variablesDataService;
        private readonly IVariablesService variablesService;
        private readonly ILogger logger;

        public DischargesController(
            IAccountService accountService,
            IAssignmentService assignmentService,
            ICommandRequestService commandRequestService,
            IDischargeService dischargeService,
            INotificationsService notificationsService,
            IHttpContextService httpContextService,
            IUnitsService unitsService,
            IVariablesDataService variablesDataService,
            IVariablesService variablesService,
            ILogger logger
        ) {
            this.accountService = accountService;
            this.assignmentService = assignmentService;
            this.commandRequestService = commandRequestService;
            this.dischargeService = dischargeService;
            this.notificationsService = notificationsService;
            this.httpContextService = httpContextService;
            this.unitsService = unitsService;
            this.variablesDataService = variablesDataService;
            this.variablesService = variablesService;
            this.logger = logger;
        }

        [HttpGet]
        public IActionResult Get() {
            IEnumerable<DischargeCollection> discharges = dischargeService.Data.Get();
            foreach (DischargeCollection discharge in discharges) {
                discharge.requestExists = commandRequestService.DoesEquivalentRequestExist(
                    new CommandRequest { recipient = discharge.accountId, type = CommandRequestType.REINSTATE_MEMBER, displayValue = "Member", displayFrom = "Discharged" }
                );
            }

            return Ok(discharges);
        }

        [HttpGet("reinstate/{id}")]
        public async Task<IActionResult> Reinstate(string id) {
            DischargeCollection dischargeCollection = dischargeService.Data.GetSingle(id);
            await dischargeService.Data.Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.reinstated, true));
            await accountService.Data.Update(dischargeCollection.accountId, "membershipState", MembershipState.MEMBER);
            Notification notification = await assignmentService.UpdateUnitRankAndRole(
                dischargeCollection.accountId,
                "Basic Training Unit",
                "Trainee",
                "Recruit",
                "",
                "",
                "your membership was reinstated"
            );
            notificationsService.Add(notification);

            logger.LogAudit($"{httpContextService.GetUserId()} reinstated {dischargeCollection.name}'s membership", httpContextService.GetUserId());
            string personnelId = variablesDataService.GetSingle("UNIT_ID_PERSONNEL").AsString();
            foreach (string member in unitsService.Data.GetSingle(personnelId).members.Where(x => x != httpContextService.GetUserId())) {
                notificationsService.Add(
                    new Notification { owner = member, icon = NotificationIcons.PROMOTION, message = $"{dischargeCollection.name}'s membership was reinstated by {httpContextService.GetUserId()}" }
                );
            }

            return Ok(dischargeService.Data.Get());
        }
    }
}
