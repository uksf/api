using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class RecruitmentController : Controller {
        private readonly IAccountContext _accountContext;
        private readonly IAccountService _accountService;
        private readonly IAssignmentService _assignmentService;
        private readonly IDisplayNameService _displayNameService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;
        private readonly IRecruitmentService _recruitmentService;

        public RecruitmentController(
            IAccountContext accountContext,
            IAccountService accountService,
            IRecruitmentService recruitmentService,
            IAssignmentService assignmentService,
            IDisplayNameService displayNameService,
            INotificationsService notificationsService,
            IHttpContextService httpContextService,
            ILogger logger
        ) {
            _accountContext = accountContext;
            _accountService = accountService;
            _recruitmentService = recruitmentService;
            _assignmentService = assignmentService;
            _displayNameService = displayNameService;
            _notificationsService = notificationsService;
            _httpContextService = httpContextService;
            _logger = logger;
        }

        [HttpGet, Authorize, Permissions(Permissions.RECRUITER)]
        public IActionResult GetAll() => Ok(_recruitmentService.GetAllApplications());

        [HttpGet("{id}"), Authorize]
        public IActionResult GetSingle(string id) {
            Account account = _accountContext.GetSingle(id);
            return Ok(_recruitmentService.GetApplication(account));
        }

        [HttpGet("isrecruiter"), Authorize, Permissions(Permissions.RECRUITER)]
        public IActionResult GetIsRecruiter() => Ok(new { recruiter = _recruitmentService.IsRecruiter(_accountService.GetUserAccount()) });

        [HttpGet("stats"), Authorize, Permissions(Permissions.RECRUITER)]
        public IActionResult GetRecruitmentStats() {
            string account = _httpContextService.GetUserId();
            List<object> activity = new();
            foreach (Account recruiterAccount in _recruitmentService.GetRecruiters()) {
                List<Account> recruiterApplications = _accountContext.Get(x => x.Application != null && x.Application.Recruiter == recruiterAccount.Id).ToList();
                activity.Add(
                    new {
                        account = new { id = recruiterAccount.Id, settings = recruiterAccount.Settings },
                        name = _displayNameService.GetDisplayName(recruiterAccount),
                        active = recruiterApplications.Count(x => x.Application.State == ApplicationState.WAITING),
                        accepted = recruiterApplications.Count(x => x.Application.State == ApplicationState.ACCEPTED),
                        rejected = recruiterApplications.Count(x => x.Application.State == ApplicationState.REJECTED)
                    }
                );
            }

            return Ok(
                new {
                    activity,
                    yourStats = new { lastMonth = _recruitmentService.GetStats(account, true), overall = _recruitmentService.GetStats(account, false) },
                    sr1Stats = new { lastMonth = _recruitmentService.GetStats("", true), overall = _recruitmentService.GetStats("", false) }
                }
            );
        }

        [HttpPost("{id}"), Authorize, Permissions(Permissions.RECRUITER)]
        public async Task<IActionResult> UpdateState([FromBody] dynamic body, string id) {
            ApplicationState updatedState = body.updatedState;
            Account account = _accountContext.GetSingle(id);
            if (updatedState == account.Application.State) return Ok();
            string sessionId = _httpContextService.GetUserId();
            await _accountContext.Update(id, Builders<Account>.Update.Set(x => x.Application.State, updatedState));
            _logger.LogAudit($"Application state changed for {id} from {account.Application.State} to {updatedState}");

            switch (updatedState) {
                case ApplicationState.ACCEPTED: {
                    await _accountContext.Update(id, Builders<Account>.Update.Set(x => x.Application.DateAccepted, DateTime.Now).Set(x => x.MembershipState, MembershipState.MEMBER));
                    Notification notification = await _assignmentService.UpdateUnitRankAndRole(id, "Basic Training Unit", "Trainee", "Recruit", reason: "your application was accepted");
                    _notificationsService.Add(notification);
                    break;
                }
                case ApplicationState.REJECTED: {
                    await _accountContext.Update(id, Builders<Account>.Update.Set(x => x.Application.DateAccepted, DateTime.Now).Set(x => x.MembershipState, MembershipState.CONFIRMED));
                    Notification notification = await _assignmentService.UpdateUnitRankAndRole(
                        id,
                        AssignmentService.REMOVE_FLAG,
                        AssignmentService.REMOVE_FLAG,
                        AssignmentService.REMOVE_FLAG,
                        "",
                        "Unfortunately you have not been accepted into our unit, however we thank you for your interest and hope you find a suitable alternative."
                    );
                    notification.Link = "/application";
                    _notificationsService.Add(notification);
                    break;
                }
                case ApplicationState.WAITING: {
                    await _accountContext.Update(
                        id,
                        Builders<Account>.Update.Set(x => x.Application.DateCreated, DateTime.Now).Unset(x => x.Application.DateAccepted).Set(x => x.MembershipState, MembershipState.CONFIRMED)
                    );
                    Notification notification = await _assignmentService.UpdateUnitRankAndRole(id, AssignmentService.REMOVE_FLAG, "Applicant", "Candidate", reason: "your application was reactivated");
                    _notificationsService.Add(notification);
                    if (_recruitmentService.GetRecruiters().All(x => x.Id != account.Application.Recruiter)) {
                        string newRecruiterId = _recruitmentService.GetRecruiter();
                        _logger.LogAudit($"Application recruiter for {id} is no longer SR1, reassigning from {account.Application.Recruiter} to {newRecruiterId}");
                        await _accountContext.Update(id, Builders<Account>.Update.Set(x => x.Application.Recruiter, newRecruiterId));
                    }

                    break;
                }
                default: throw new ArgumentOutOfRangeException();
            }

            account = _accountContext.GetSingle(id);
            string message = updatedState == ApplicationState.WAITING ? "was reactivated" : $"was {updatedState}";
            if (sessionId != account.Application.Recruiter) {
                _notificationsService.Add(
                    new Notification {
                        Owner = account.Application.Recruiter,
                        Icon = NotificationIcons.APPLICATION,
                        Message = $"{account.Firstname} {account.Lastname}'s application {message} by {_displayNameService.GetDisplayName(_accountService.GetUserAccount())}",
                        Link = $"/recruitment/{id}"
                    }
                );
            }

            foreach (string value in _recruitmentService.GetRecruiterLeads().Values.Where(value => sessionId != value && account.Application.Recruiter != value)) {
                _notificationsService.Add(
                    new Notification {
                        Owner = value,
                        Icon = NotificationIcons.APPLICATION,
                        Message = $"{account.Firstname} {account.Lastname}'s application {message} by {_displayNameService.GetDisplayName(_accountService.GetUserAccount())}",
                        Link = $"/recruitment/{id}"
                    }
                );
            }

            return Ok();
        }

        [HttpPost("recruiter/{id}"), Authorize, Permissions(Permissions.RECRUITER_LEAD)]
        public async Task<IActionResult> PostReassignment([FromBody] JObject newRecruiter, string id) {
            if (!_httpContextService.UserHasPermission(Permissions.ADMIN) && !_recruitmentService.IsRecruiterLead()) {
                throw new Exception($"attempted to assign recruiter to {newRecruiter}. Context is not recruitment lead.");
            }

            string recruiter = newRecruiter["newRecruiter"].ToString();
            await _recruitmentService.SetRecruiter(id, recruiter);
            Account account = _accountContext.GetSingle(id);
            if (account.Application.State == ApplicationState.WAITING) {
                _notificationsService.Add(
                    new Notification {
                        Owner = recruiter,
                        Icon = NotificationIcons.APPLICATION,
                        Message = $"{account.Firstname} {account.Lastname}'s application has been transferred to you",
                        Link = $"/recruitment/{account.Id}"
                    }
                );
            }

            _logger.LogAudit($"Application recruiter changed for {id} to {newRecruiter["newRecruiter"]}");
            return Ok();
        }

        [HttpPost("ratings/{id}"), Authorize, Permissions(Permissions.RECRUITER)]
        public async Task<Dictionary<string, uint>> Ratings([FromBody] KeyValuePair<string, uint> value, string id) {
            Dictionary<string, uint> ratings = _accountContext.GetSingle(id).Application.Ratings;

            (string key, uint rating) = value;
            if (ratings.ContainsKey(key)) {
                ratings[key] = rating;
            } else {
                ratings.Add(key, rating);
            }

            await _accountContext.Update(id, Builders<Account>.Update.Set(x => x.Application.Ratings, ratings));
            return ratings;
        }

        [HttpGet("recruiters"), Authorize, Permissions(Permissions.RECRUITER_LEAD)]
        public IEnumerable<Recruiter> GetRecruiters() => _recruitmentService.GetActiveRecruiters();
    }
}
