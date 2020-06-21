using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;
using UKSF.Common;

namespace UKSF.Api.Controllers {
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
            CommentThread recruiterCommentThread = new CommentThread {authors = recruitmentService.GetRecruiterLeads().Values.ToArray(), mode = ThreadMode.RECRUITER};
            CommentThread applicationCommentThread = new CommentThread {authors = new[] {account.id}, mode = ThreadMode.RECRUITER};
            await commentThreadService.Data.Add(recruiterCommentThread);
            await commentThreadService.Data.Add(applicationCommentThread);
            Application application = new Application {
                dateCreated = DateTime.Now,
                state = ApplicationState.WAITING,
                recruiter = recruitmentService.GetRecruiter(),
                recruiterCommentThread = recruiterCommentThread.id,
                applicationCommentThread = applicationCommentThread.id
            };
            await accountService.Data.Update(account.id, Builders<Account>.Update.Set(x => x.application, application));
            account = accountService.Data.GetSingle(account.id);
            Notification notification = await assignmentService.UpdateUnitRankAndRole(account.id, "", "Applicant", "Candidate", reason: "you were entered into the recruitment process");
            notificationsService.Add(notification);
            notificationsService.Add(new Notification {owner = application.recruiter, icon = NotificationIcons.APPLICATION, message = $"You have been assigned {account.firstname} {account.lastname}'s application", link = $"/recruitment/{account.id}"});
            foreach (string id in recruitmentService.GetRecruiterLeads().Values.Where(x => account.application.recruiter != x)) {
                notificationsService.Add(
                    new Notification {owner = id, icon = NotificationIcons.APPLICATION, message = $"{displayNameService.GetDisplayName(account.application.recruiter)} has been assigned {account.firstname} {account.lastname}'s application", link = $"/recruitment/{account.id}"}
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
            string difference = account.Changes(accountService.Data.GetSingle(account.id));
            LogWrapper.AuditLog(account.id, $"Application updated for {account.id}: {difference}");
            return Ok();
        }

        private async Task Update(JObject body, Account account) {
            await accountService.Data
                                .Update(
                                    account.id,
                                    Builders<Account>.Update.Set(x => x.armaExperience, body["armaExperience"].ToString())
                                                     .Set(x => x.unitsExperience, body["unitsExperience"].ToString())
                                                     .Set(x => x.background, body["background"].ToString())
                                                     .Set(x => x.militaryExperience, string.Equals(body["militaryExperience"].ToString(), "true", StringComparison.InvariantCultureIgnoreCase))
                                                     .Set(x => x.rolePreferences, body["rolePreferences"].ToObject<List<string>>())
                                                     .Set(x => x.reference, body["reference"].ToString())
                                );
        }
    }
}
