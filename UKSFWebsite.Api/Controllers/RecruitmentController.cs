using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Message;
using UKSFWebsite.Api.Services.Personnel;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class RecruitmentController : Controller {
        private readonly IAccountService accountService;
        private readonly IAssignmentService assignmentService;
        private readonly IDisplayNameService displayNameService;
        private readonly INotificationsService notificationsService;
        private readonly IRecruitmentService recruitmentService;
        private readonly ISessionService sessionService;

        public RecruitmentController(IAccountService accountService, IRecruitmentService recruitmentService, IAssignmentService assignmentService, ISessionService sessionService, IDisplayNameService displayNameService, INotificationsService notificationsService) {
            this.accountService = accountService;
            this.recruitmentService = recruitmentService;
            this.assignmentService = assignmentService;
            this.sessionService = sessionService;
            this.displayNameService = displayNameService;
            this.notificationsService = notificationsService;
        }

        [HttpGet, Authorize, Roles(RoleDefinitions.SR1)]
        public IActionResult GetAll() => Ok(recruitmentService.GetAllApplications());

        [HttpGet("{id}"), Authorize]
        public IActionResult GetSingle(string id) {
            Account account = accountService.Data().GetSingle(id);
            return Ok(recruitmentService.GetApplication(account));
        }

        [HttpGet("isrecruiter"), Authorize, Roles(RoleDefinitions.SR1)]
        public IActionResult GetIsRecruiter() => Ok(new {recruiter = recruitmentService.IsRecruiter(sessionService.GetContextAccount())});

        [HttpGet("stats"), Authorize, Roles(RoleDefinitions.SR1)]
        public IActionResult GetRecruitmentStats() {
            string account = sessionService.GetContextId();
            List<object> activity = new List<object>();
            foreach (Account recruiterAccount in recruitmentService.GetSr1Members()) {
                List<Account> recruiterApplications = accountService.Data().Get(x => x.application != null && x.application.recruiter == recruiterAccount.id);
                activity.Add(
                    new {
                        account = new {recruiterAccount.id, recruiterAccount.settings},
                        name = displayNameService.GetDisplayName(recruiterAccount),
                        active = recruiterApplications.Count(x => x.application.state == ApplicationState.WAITING),
                        accepted = recruiterApplications.Count(x => x.application.state == ApplicationState.ACCEPTED),
                        rejected = recruiterApplications.Count(x => x.application.state == ApplicationState.REJECTED)
                    }
                );
            }

            return Ok(new {activity, yourStats = new {lastMonth = recruitmentService.GetStats(account, true), overall = recruitmentService.GetStats(account, false)}, sr1Stats = new {lastMonth = recruitmentService.GetStats("", true), overall = recruitmentService.GetStats("", false)}});
        }

        [HttpPost("{id}"), Authorize, Roles(RoleDefinitions.SR1)]
        public async Task<IActionResult> UpdateState([FromBody] dynamic body, string id) {
            ApplicationState updatedState = body.updatedState;
            Account account = accountService.Data().GetSingle(id);
            if (updatedState == account.application.state) return Ok();
            string sessionId = sessionService.GetContextId();
            await accountService.Data().Update(id, Builders<Account>.Update.Set(x => x.application.state, updatedState));
            LogWrapper.AuditLog(sessionId, $"Application state changed for {id} from {account.application.state} to {updatedState}");

            switch (updatedState) {
                case ApplicationState.ACCEPTED: {
                    await accountService.Data().Update(id, Builders<Account>.Update.Set(x => x.application.dateAccepted, DateTime.Now).Set(x => x.membershipState, MembershipState.MEMBER));
                    Notification notification = await assignmentService.UpdateUnitRankAndRole(id, "Basic Training Unit", "Trainee", "Recruit", reason: "your application was accepted");
                    notificationsService.Add(notification);
                    break;
                }
                case ApplicationState.REJECTED: {
                    await accountService.Data().Update(id, Builders<Account>.Update.Set(x => x.application.dateAccepted, DateTime.Now).Set(x => x.membershipState, MembershipState.CONFIRMED));
                    Notification notification = await assignmentService.UpdateUnitRankAndRole(
                        id,
                        AssignmentService.REMOVE_FLAG,
                        AssignmentService.REMOVE_FLAG,
                        AssignmentService.REMOVE_FLAG,
                        "",
                        $"Unfortunately you have not been accepted into our unit, however we thank you for your interest and hope you find a suitable alternative. You can view any comments on your application here: [url]https://uk-sf.co.uk/recruitment/{id}[/url]"
                    );
                    notificationsService.Add(notification);
                    break;
                }
                case ApplicationState.WAITING: {
                    await accountService.Data().Update(id, Builders<Account>.Update.Set(x => x.application.dateCreated, DateTime.Now).Unset(x => x.application.dateAccepted).Set(x => x.membershipState, MembershipState.CONFIRMED));
                    Notification notification = await assignmentService.UpdateUnitRankAndRole(id, AssignmentService.REMOVE_FLAG, "Applicant", "Candidate", reason: "your application was reactivated");
                    notificationsService.Add(notification);
                    if (recruitmentService.GetSr1Members().All(x => x.id != account.application.recruiter)) {
                        string newRecruiterId = recruitmentService.GetRecruiter();
                        LogWrapper.AuditLog(sessionId, $"Application recruiter for {id} is no longer SR1, reassigning from {account.application.recruiter} to {newRecruiterId}");
                        await accountService.Data().Update(id, Builders<Account>.Update.Set(x => x.application.recruiter, newRecruiterId));
                    }

                    break;
                }
                default: throw new ArgumentOutOfRangeException();
            }

            account = accountService.Data().GetSingle(id);
            string message = updatedState == ApplicationState.WAITING ? "was reactivated" : $"was {updatedState}";
            if (sessionId != account.application.recruiter) {
                notificationsService.Add(
                    new Notification {owner = account.application.recruiter, icon = NotificationIcons.APPLICATION, message = $"{account.firstname} {account.lastname}'s application {message} by {displayNameService.GetDisplayName(sessionService.GetContextAccount())}", link = $"/recruitment/{id}"}
                );
            }

            foreach (string value in recruitmentService.GetSr1Leads().Values.Where(value => sessionId != value && account.application.recruiter != value)) {
                notificationsService.Add(new Notification {owner = value, icon = NotificationIcons.APPLICATION, message = $"{account.firstname} {account.lastname}'s application {message} by {displayNameService.GetDisplayName(sessionService.GetContextAccount())}", link = $"/recruitment/{id}"});
            }

            return Ok();
        }

        [HttpPost("recruiter/{id}"), Authorize, Roles(RoleDefinitions.SR1_LEAD)]
        public async Task<IActionResult> PostReassignment([FromBody] JObject newRecruiter, string id) {
            if (!sessionService.ContextHasRole(RoleDefinitions.ADMIN) && !recruitmentService.IsAccountSr1Lead()) throw new Exception($"attempted to assign recruiter to {newRecruiter}. Context is not recruitment lead.");
            string recruiter = newRecruiter["newRecruiter"].ToString();
            await recruitmentService.SetRecruiter(id, recruiter);
            Account account = accountService.Data().GetSingle(id);
            if (account.application.state == ApplicationState.WAITING) {
                notificationsService.Add(new Notification {owner = recruiter, icon = NotificationIcons.APPLICATION, message = $"{account.firstname} {account.lastname}'s application has been transferred to you", link = $"/recruitment/{account.id}"});
            }

            LogWrapper.AuditLog(sessionService.GetContextId(), $"Application recruiter changed for {id} to {newRecruiter["newRecruiter"]}");
            return Ok();
        }

        [HttpPost("ratings/{id}"), Authorize, Roles(RoleDefinitions.SR1)]
        public async Task<Dictionary<string, uint>> Ratings([FromBody] KeyValuePair<string, uint> value, string id) {
            Dictionary<string, uint> ratings = accountService.Data().GetSingle(id).application.ratings;

            (string key, uint rating) = value;
            if (ratings.ContainsKey(key)) {
                ratings[key] = rating;
            } else {
                ratings.Add(key, rating);
            }

            await accountService.Data().Update(id, Builders<Account>.Update.Set(x => x.application.ratings, ratings));
            return ratings;
        }

        [HttpGet("recruiters"), Authorize, Roles(RoleDefinitions.SR1_LEAD)]
        public IActionResult GetRecruiters() => Ok(recruitmentService.GetActiveRecruiters());
    }
}
