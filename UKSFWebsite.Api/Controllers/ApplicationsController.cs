using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class ApplicationsController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly ICommentThreadService commentThreadService;
        private readonly IDisplayNameService displayNameService;
        private readonly INotificationsService notificationsService;
        private readonly IRecruitmentService recruitmentService;
        private readonly ISessionService sessionService;

        public ApplicationsController(
            IRecruitmentService recruitmentService,
            IAssignmentService assignmentService,
            ISessionService sessionService,
            IAccountService accountService,
            ICommentThreadService commentThreadService,
            INotificationsService notificationsService,
            IDisplayNameService displayNameService
        ) {
            this.assignmentService = assignmentService;
            this.recruitmentService = recruitmentService;
            this.sessionService = sessionService;
            this.accountService = accountService;
            this.commentThreadService = commentThreadService;
            this.notificationsService = notificationsService;
            this.displayNameService = displayNameService;
        }

        [HttpPost, Authorize, Roles(RoleDefinitions.CONFIRMED)]
        public async Task<IActionResult> Post([FromBody] JObject body) {
            Account account = sessionService.GetContextAccount();
            await Update(body, account);
            Application application = new Application {
                dateCreated = DateTime.Now,
                state = ApplicationState.WAITING,
                recruiter = recruitmentService.GetRecruiter(),
                recruiterCommentThread = await commentThreadService.Add(new CommentThread {authors = recruitmentService.GetSr1Leads().Values.ToArray(), mode = ThreadMode.SR1}),
                applicationCommentThread = await commentThreadService.Add(new CommentThread {authors = new[] {account.id}, mode = ThreadMode.SR1})
            };
            await accountService.Update(account.id, Builders<Account>.Update.Set(x => x.application, application));
            account = accountService.GetSingle(account.id);
            Notification notification = await assignmentService.UpdateUnitRankAndRole(account.id, "", "Applicant", "Candidate", reason: "you were entered into the recruitment process");
            notificationsService.Add(notification);
            notificationsService.Add(new Notification {owner = application.recruiter, icon = NotificationIcons.APPLICATION, message = $"You have been assigned {account.firstname} {account.lastname}'s application", link = $"/recruitment/{account.id}"});
            foreach ((_, string sr1Id) in recruitmentService.GetSr1Leads()) {
                if (account.application.recruiter == sr1Id) continue;
                notificationsService.Add(
                    new Notification {
                        owner = sr1Id, icon = NotificationIcons.APPLICATION, message = $"{displayNameService.GetDisplayName(account.application.recruiter)} has been assigned {account.firstname} {account.lastname}'s application", link = $"/recruitment/{account.id}"
                    }
                );
            }

            LogWrapper.AuditLog(account.id, $"Application submitted for {account.id}. Assigned to {displayNameService.GetDisplayName(account.application.recruiter)}");
            return Ok();
        }

        [HttpPost("update"), Authorize, Roles(RoleDefinitions.CONFIRMED)]
        public async Task<IActionResult> PostUpdate([FromBody] JObject body) {
            Account account = sessionService.GetContextAccount();
            await Update(body, account);
            notificationsService.Add(new Notification {owner = account.application.recruiter, icon = NotificationIcons.APPLICATION, message = $"{account.firstname} {account.lastname} updated their application", link = $"/recruitment/{account.id}"});
            string difference = account.Changes(accountService.GetSingle(account.id));
            LogWrapper.AuditLog(account.id, $"Application updated for {account.id}: {difference}");
            return Ok();
        }

        private async Task Update(JObject body, Account account) {
            await accountService.Update(
                account.id,
                Builders<Account>.Update.Set(x => x.armaExperience, body["armaExperience"].ToString())
                                 .Set(x => x.unitsExperience, body["unitsExperience"].ToString())
                                 .Set(x => x.background, body["background"].ToString())
                                 .Set(x => x.militaryExperience, string.Equals(body["militaryExperience"].ToString(), "true", StringComparison.InvariantCultureIgnoreCase))
                                 .Set(x => x.officer, string.Equals(body["officer"].ToString(), "true", StringComparison.InvariantCultureIgnoreCase))
                                 .Set(x => x.nco, string.Equals(body["nco"].ToString(), "true", StringComparison.InvariantCultureIgnoreCase))
                                 .Set(x => x.aviation, string.Equals(body["aviation"].ToString(), "true", StringComparison.InvariantCultureIgnoreCase))
                                 .Set(x => x.reference, body["reference"].ToString())
            );
        }
    }
}
