using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Utility;

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
            Account account = accountService.GetSingle(id);
            return Ok(recruitmentService.GetApplication(account));
        }

        [HttpGet("isrecruiter"), Authorize, Roles(RoleDefinitions.SR1)]
        public IActionResult GetIsRecruiter() => Ok(new {recruiter = recruitmentService.IsRecruiter(sessionService.GetContextAccount())});

        [HttpGet("stats"), Authorize, Roles(RoleDefinitions.SR1)]
        public IActionResult GetRecruitmentStats() {
            string account = sessionService.GetContextId();
            List<object> activity = new List<object>();
            foreach (Account recruiterAccount in recruitmentService.GetSr1Members()) {
                List<Account> recruiterApplications = accountService.Get(x => x.application != null && x.application.recruiter == recruiterAccount.id);
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
            Account account = accountService.GetSingle(id);
            if (updatedState == account.application.state) return Ok();
            string sessionId = sessionService.GetContextId();
            await accountService.Update(id, Builders<Account>.Update.Set(x => x.application.state, updatedState));
            LogWrapper.AuditLog(sessionId, $"Application state changed for {id} from {account.application.state} to {updatedState}");

            if (updatedState == ApplicationState.ACCEPTED) {
                await accountService.Update(id, Builders<Account>.Update.Set(x => x.application.dateAccepted, DateTime.Now));
                await accountService.Update(id, "membershipState", MembershipState.MEMBER);
                await assignmentService.UpdateUnitRankAndRole(id, "Basic Training Unit", "Trainee", "Recruit", reason: "your application was accepted");
            } else if (updatedState == ApplicationState.REJECTED) {
                await accountService.Update(id, Builders<Account>.Update.Set(x => x.application.dateAccepted, DateTime.Now));
                await accountService.Update(id, "membershipState", MembershipState.CONFIRMED);
                await assignmentService.UpdateUnitRankAndRole(
                    id,
                    AssignmentService.REMOVE_FLAG,
                    AssignmentService.REMOVE_FLAG,
                    AssignmentService.REMOVE_FLAG,
                    "",
                    $"Unfortunately you have not been accepted into our unit, however we thank you for your interest and hope you find a suitable alternative. You may view any notes about your application here: [url]https://uk-sf.co.uk/recruitment/{id}[/url]"
                );
            } else if (updatedState == ApplicationState.WAITING) {
                await accountService.Update(id, Builders<Account>.Update.Set(x => x.application.dateCreated, DateTime.Now));
                await accountService.Update(id, Builders<Account>.Update.Unset(x => x.application.dateAccepted));
                await accountService.Update(id, "membershipState", MembershipState.CONFIRMED);
                await assignmentService.UpdateUnitRankAndRole(id, AssignmentService.REMOVE_FLAG, "Applicant", "Candidate", reason: "your application was reactivated");
                if (recruitmentService.GetSr1Members().All(x => x.id != account.application.recruiter)) {
                    string newRecruiterId = recruitmentService.GetRecruiter();
                    LogWrapper.AuditLog(sessionId, $"Application recruiter for {id} is no longer SR1, reassigning from {account.application.recruiter} to {newRecruiterId}");
                    await accountService.Update(id, Builders<Account>.Update.Set(x => x.application.recruiter, newRecruiterId));
                }
            }

            account = accountService.GetSingle(id);
            string message = updatedState == ApplicationState.WAITING ? "was reactivated" : $"was {updatedState}";
            if (sessionId != account.application.recruiter) {
                notificationsService.Add(
                    new Notification {owner = account.application.recruiter, icon = NotificationIcons.APPLICATION, message = $"{account.firstname} {account.lastname}'s application {message} by {displayNameService.GetDisplayName(sessionService.GetContextAccount())}", link = $"/recruitment/{id}"}
                );
            }

            foreach ((_, string value) in recruitmentService.GetSr1Leads()) {
                if (sessionId == value || account.application.recruiter == value) continue;
                notificationsService.Add(new Notification {owner = value, icon = NotificationIcons.APPLICATION, message = $"{account.firstname} {account.lastname}'s application {message} by {displayNameService.GetDisplayName(sessionService.GetContextAccount())}", link = $"/recruitment/{id}"});
            }

            return Ok();
        }

        [HttpPost("recruiter/{id}"), Authorize, Roles(RoleDefinitions.SR1_LEAD)]
        public async Task<IActionResult> PostReassignment([FromBody] JObject newRecruiter, string id) {
            if (!sessionService.ContextHasRole(RoleDefinitions.ADMIN) && !recruitmentService.IsAccountSr1Lead()) throw new Exception($"attempted to assign recruiter to {newRecruiter}. Context is not recruitment lead.");
            await recruitmentService.SetRecruiter(id, newRecruiter["newRecruiter"].ToString());
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Application recruiter changed for {id} to {newRecruiter["newRecruiter"]}");
            return Ok();
        }

        [HttpPost("ratings/{id}"), Authorize, Roles(RoleDefinitions.SR1)]
        public async Task<Dictionary<string, uint>> Ratings([FromBody] KeyValuePair<string, uint> value, string id) {
            Dictionary<string, uint> ratings = accountService.GetSingle(id).application.ratings;

            (string key, uint rating) = value;
            if (ratings.ContainsKey(key)) {
                ratings[key] = rating;
            } else {
                ratings.Add(key, rating);
            }

            await accountService.Update(id, Builders<Account>.Update.Set(x => x.application.ratings, ratings));
            return ratings;
        }

        [HttpGet("recruiters/{id}"), Authorize, Roles(RoleDefinitions.SR1_LEAD)]
        public IActionResult GetRecruiters(string id) {
            Account account = accountService.GetSingle(id);
            return Ok(recruitmentService.GetOtherRecruiters(account.application.recruiter));
        }
    }
}
