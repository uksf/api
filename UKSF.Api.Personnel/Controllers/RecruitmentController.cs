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
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Controllers
{
    [Route("[controller]")]
    public class RecruitmentController : ControllerBase
    {
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
        )
        {
            _accountContext = accountContext;
            _accountService = accountService;
            _recruitmentService = recruitmentService;
            _assignmentService = assignmentService;
            _displayNameService = displayNameService;
            _notificationsService = notificationsService;
            _httpContextService = httpContextService;
            _logger = logger;
        }

        [HttpGet, Permissions(Permissions.RECRUITER)]
        public ApplicationsOverview GetAll()
        {
            return _recruitmentService.GetAllApplications();
        }

        [HttpGet("{id}"), Authorize]
        public DetailedApplication GetSingle(string id)
        {
            DomainAccount domainAccount = _accountContext.GetSingle(id);
            return _recruitmentService.GetApplication(domainAccount);
        }

        [HttpGet("isrecruiter"), Permissions(Permissions.RECRUITER)]
        public bool GetIsRecruiter()
        {
            return _recruitmentService.IsRecruiter(_accountService.GetUserAccount());
        }

        [HttpGet("stats"), Permissions(Permissions.RECRUITER)]
        public RecruitmentStatsDataset GetRecruitmentStats()
        {
            string account = _httpContextService.GetUserId();
            List<RecruitmentActivityDataset> activity = new();
            foreach (DomainAccount recruiterAccount in _recruitmentService.GetRecruiters())
            {
                List<DomainAccount> recruiterApplications = _accountContext.Get(x => x.Application != null && x.Application.Recruiter == recruiterAccount.Id).ToList();
                activity.Add(
                    new()
                    {
                        Account = new { id = recruiterAccount.Id, settings = recruiterAccount.Settings },
                        Name = _displayNameService.GetDisplayName(recruiterAccount),
                        Active = recruiterApplications.Count(x => x.Application.State == ApplicationState.WAITING),
                        Accepted = recruiterApplications.Count(x => x.Application.State == ApplicationState.ACCEPTED),
                        Rejected = recruiterApplications.Count(x => x.Application.State == ApplicationState.REJECTED)
                    }
                );
            }

            return new()
            {
                Activity = activity,
                YourStats = new() { LastMonth = _recruitmentService.GetStats(account, true), Overall = _recruitmentService.GetStats(account, false) },
                Sr1Stats = new() { LastMonth = _recruitmentService.GetStats("", true), Overall = _recruitmentService.GetStats("", false) }
            };
        }

        [HttpPost("{id}"), Permissions(Permissions.RECRUITER)]
        public async Task UpdateState([FromBody] dynamic body, string id)
        {
            ApplicationState updatedState = body.updatedState;
            DomainAccount domainAccount = _accountContext.GetSingle(id);
            if (updatedState == domainAccount.Application.State)
            {
                return;
            }

            string sessionId = _httpContextService.GetUserId();
            await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.Application.State, updatedState));
            _logger.LogAudit($"Application state changed for {id} from {domainAccount.Application.State} to {updatedState}");

            switch (updatedState)
            {
                case ApplicationState.ACCEPTED:
                {
                    await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.Application.DateAccepted, DateTime.Now).Set(x => x.MembershipState, MembershipState.MEMBER));
                    Notification notification = await _assignmentService.UpdateUnitRankAndRole(id, "Basic Training Unit", "Trainee", "Recruit", reason: "your application was accepted");
                    _notificationsService.Add(notification);
                    break;
                }
                case ApplicationState.REJECTED:
                {
                    await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.Application.DateAccepted, DateTime.Now).Set(x => x.MembershipState, MembershipState.CONFIRMED));
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
                case ApplicationState.WAITING:
                {
                    await _accountContext.Update(
                        id,
                        Builders<DomainAccount>.Update.Set(x => x.Application.DateCreated, DateTime.Now).Unset(x => x.Application.DateAccepted).Set(x => x.MembershipState, MembershipState.CONFIRMED)
                    );
                    Notification notification = await _assignmentService.UpdateUnitRankAndRole(id, AssignmentService.REMOVE_FLAG, "Applicant", "Candidate", reason: "your application was reactivated");
                    _notificationsService.Add(notification);
                    if (_recruitmentService.GetRecruiters().All(x => x.Id != domainAccount.Application.Recruiter))
                    {
                        string newRecruiterId = _recruitmentService.GetRecruiter();
                        _logger.LogAudit($"Application recruiter for {id} is no longer SR1, reassigning from {domainAccount.Application.Recruiter} to {newRecruiterId}");
                        await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.Application.Recruiter, newRecruiterId));
                    }

                    break;
                }
                default: throw new BadRequestException($"New state {updatedState} is invalid");
            }

            domainAccount = _accountContext.GetSingle(id);
            string message = updatedState == ApplicationState.WAITING ? "was reactivated" : $"was {updatedState}";
            if (sessionId != domainAccount.Application.Recruiter)
            {
                _notificationsService.Add(
                    new()
                    {
                        Owner = domainAccount.Application.Recruiter,
                        Icon = NotificationIcons.APPLICATION,
                        Message = $"{domainAccount.Firstname} {domainAccount.Lastname}'s application {message} by {_displayNameService.GetDisplayName(_accountService.GetUserAccount())}",
                        Link = $"/recruitment/{id}"
                    }
                );
            }

            foreach (string value in _recruitmentService.GetRecruiterLeads().Values.Where(value => sessionId != value && domainAccount.Application.Recruiter != value))
            {
                _notificationsService.Add(
                    new()
                    {
                        Owner = value,
                        Icon = NotificationIcons.APPLICATION,
                        Message = $"{domainAccount.Firstname} {domainAccount.Lastname}'s application {message} by {_displayNameService.GetDisplayName(_accountService.GetUserAccount())}",
                        Link = $"/recruitment/{id}"
                    }
                );
            }
        }

        [HttpPost("recruiter/{id}"), Permissions(Permissions.RECRUITER_LEAD)]
        public async Task PostReassignment([FromBody] JObject newRecruiter, string id)
        {
            if (!_httpContextService.UserHasPermission(Permissions.ADMIN) && !_recruitmentService.IsRecruiterLead())
            {
                throw new($"attempted to assign recruiter to {newRecruiter}. Context is not recruitment lead.");
            }

            string recruiter = newRecruiter["newRecruiter"].ToString();
            await _recruitmentService.SetRecruiter(id, recruiter);
            DomainAccount domainAccount = _accountContext.GetSingle(id);
            if (domainAccount.Application.State == ApplicationState.WAITING)
            {
                _notificationsService.Add(
                    new()
                    {
                        Owner = recruiter,
                        Icon = NotificationIcons.APPLICATION,
                        Message = $"{domainAccount.Firstname} {domainAccount.Lastname}'s application has been transferred to you",
                        Link = $"/recruitment/{domainAccount.Id}"
                    }
                );
            }

            _logger.LogAudit($"Application recruiter changed for {id} to {newRecruiter["newRecruiter"]}");
        }

        [HttpPost("ratings/{id}"), Permissions(Permissions.RECRUITER)]
        public async Task<Dictionary<string, uint>> Ratings([FromBody] KeyValuePair<string, uint> value, string id)
        {
            Dictionary<string, uint> ratings = _accountContext.GetSingle(id).Application.Ratings;

            (string key, uint rating) = value;
            if (ratings.ContainsKey(key))
            {
                ratings[key] = rating;
            }
            else
            {
                ratings.Add(key, rating);
            }

            await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.Application.Ratings, ratings));
            return ratings;
        }

        [HttpGet("recruiters"), Permissions(Permissions.RECRUITER_LEAD)]
        public IEnumerable<Recruiter> GetRecruiters()
        {
            return _recruitmentService.GetActiveRecruiters();
        }
    }
}
