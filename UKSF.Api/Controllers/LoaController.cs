using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Permissions(Permissions.MEMBER)]
    public class LoaController : Controller {
        private readonly IAccountService accountService;
        private readonly IChainOfCommandService chainOfCommandService;
        private readonly ICommandRequestService commandRequestService;
        private readonly IDisplayNameService displayNameService;
        private readonly ILoaService loaService;
        private readonly IHttpContextService httpContextService;
        private readonly INotificationsService notificationsService;
        private readonly ILogger logger;

        private readonly IUnitsService unitsService;

        public LoaController(
            ILoaService loaService,
            IHttpContextService httpContextService,
            IDisplayNameService displayNameService,
            IAccountService accountService,
            IUnitsService unitsService,
            IChainOfCommandService chainOfCommandService,
            ICommandRequestService commandRequestService,
            INotificationsService notificationsService,
            ILogger logger
        ) {
            this.loaService = loaService;
            this.httpContextService = httpContextService;
            this.displayNameService = displayNameService;
            this.accountService = accountService;
            this.unitsService = unitsService;
            this.chainOfCommandService = chainOfCommandService;
            this.commandRequestService = commandRequestService;
            this.notificationsService = notificationsService;
            this.logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult Get([FromQuery] string scope = "you") {
            List<string> objectIds;
            switch (scope) {
                case "all":
                    objectIds = accountService.Data.Get(x => x.membershipState == MembershipState.MEMBER).Select(x => x.id).ToList();
                    break;
                case "unit":
                    Account account = accountService.GetUserAccount();
                    IEnumerable<Unit> groups = unitsService.GetAllChildren(unitsService.Data.GetSingle(x => x.name == account.unitAssignment), true);
                    List<string> members = groups.SelectMany(x => x.members.ToList()).ToList();
                    objectIds = accountService.Data.Get(x => x.membershipState == MembershipState.MEMBER && members.Contains(x.id)).Select(x => x.id).ToList();
                    break;
                case "you":
                    objectIds = new List<string> {httpContextService.GetUserId()};
                    break;
                default: return BadRequest();
            }

            IEnumerable<dynamic> loaReports = loaService.Get(objectIds)
                                                        .Select(
                                                            x => new {
                                                                x.id,
                                                                x.start,
                                                                x.end,
                                                                x.state,
                                                                x.emergency,
                                                                x.late,
                                                                x.reason,
                                                                name = displayNameService.GetDisplayName(accountService.Data.GetSingle(x.recipient)),
                                                                inChainOfCommand = chainOfCommandService.InContextChainOfCommand(x.recipient),
                                                                longTerm = (x.end - x.start).Days > 21
                                                            }
                                                        )
                                                        .ToList();
            return Ok(
                new {
                    activeLoas = loaReports.Where(x => x.start <= DateTime.Now && x.end > DateTime.Now).OrderBy(x => x.end).ThenBy(x => x.start),
                    upcomingLoas = loaReports.Where(x => x.start >= DateTime.Now).OrderBy(x => x.start).ThenBy(x => x.end),
                    pastLoas = loaReports.Where(x => x.end < DateTime.Now).OrderByDescending(x => x.end).ThenByDescending(x => x.start)
                }
            );
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteLoa(string id) {
            Loa loa = loaService.Data.GetSingle(id);
            CommandRequest request = commandRequestService.Data.GetSingle(x => x.Value == id);
            if (request != null) {
                await commandRequestService.Data.Delete(request);
                foreach (string reviewerId in request.Reviews.Keys.Where(x => x != request.Requester)) {
                    notificationsService.Add(new Notification {owner = reviewerId, icon = NotificationIcons.REQUEST, message = $"Your review for {request.DisplayRequester}'s LOA is no longer required as they deleted their LOA", link = "/command/requests"});
                }

                logger.LogAudit($"Loa request deleted for '{displayNameService.GetDisplayName(accountService.Data.GetSingle(loa.recipient))}' from '{loa.start}' to '{loa.end}'");
            }

            logger.LogAudit($"Loa deleted for '{displayNameService.GetDisplayName(accountService.Data.GetSingle(loa.recipient))}' from '{loa.start}' to '{loa.end}'");
            await loaService.Data.Delete(loa);

            return Ok();
        }
    }
}
