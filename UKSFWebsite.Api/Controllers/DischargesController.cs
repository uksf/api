using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Command;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Command;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Message;
using UKSFWebsite.Api.Services.Personnel;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.SR10, RoleDefinitions.NCO, RoleDefinitions.SR1)]
    public class DischargesController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly ICommandRequestService commandRequestService;
        private readonly IDischargeService dischargeService;
        private readonly INotificationsService notificationsService;
        private readonly ISessionService sessionService;
        private readonly IUnitsService unitsService;

        public DischargesController(IAccountService accountService, IAssignmentService assignmentService, ICommandRequestService commandRequestService, IDischargeService dischargeService, INotificationsService notificationsService, ISessionService sessionService, IUnitsService unitsService) {
            this.accountService = accountService;
            this.assignmentService = assignmentService;
            this.commandRequestService = commandRequestService;
            this.dischargeService = dischargeService;
            this.notificationsService = notificationsService;
            this.sessionService = sessionService;
            this.unitsService = unitsService;
        }

        [HttpGet]
        public IActionResult Get() {
            IEnumerable<DischargeCollection> discharges = dischargeService.Data().Get();
            foreach (DischargeCollection discharge in discharges) {
                discharge.requestExists = commandRequestService.DoesEquivalentRequestExist(new CommandRequest {recipient = discharge.accountId, type = CommandRequestType.REINSTATE_MEMBER, displayValue = "Member", displayFrom = "Discharged"});
            }

            return Ok(discharges);
        }

        [HttpGet("reinstate/{id}")]
        public async Task<IActionResult> Reinstate(string id) {
            DischargeCollection dischargeCollection = dischargeService.Data().GetSingle(id);
            await dischargeService.Data().Update(dischargeCollection.id, Builders<DischargeCollection>.Update.Set(x => x.reinstated, true));
            await accountService.Data().Update(dischargeCollection.accountId, "membershipState", MembershipState.MEMBER);
            Notification notification = await assignmentService.UpdateUnitRankAndRole(dischargeCollection.accountId, "Basic Training Unit", "Trainee", "Recruit", "", "", "your membership was reinstated");
            notificationsService.Add(notification);

            LogWrapper.AuditLog(sessionService.GetContextId(), $"{sessionService.GetContextId()} reinstated {dischargeCollection.name}'s membership");
            foreach (string member in unitsService.Data().GetSingle(x => x.shortname == "SR10").members.Where(x => x != sessionService.GetContextId())) {
                notificationsService.Add(new Notification {owner = member, icon = NotificationIcons.PROMOTION, message = $"{dischargeCollection.name}'s membership was reinstated by {sessionService.GetContextId()}"});
            }

            return Ok(dischargeService.Data().Get());
        }
    }
}
