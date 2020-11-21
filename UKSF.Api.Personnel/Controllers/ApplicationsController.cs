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
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Personnel.Controllers {
    [Route("[controller]")]
    public class ApplicationsController : Controller {
        private readonly IAccountContext _accountContext;
        private readonly IAccountService _accountService;
        private readonly IAssignmentService _assignmentService;
        private readonly ICommentThreadContext _commentThreadContext;
        private readonly IDisplayNameService _displayNameService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;
        private readonly IRecruitmentService _recruitmentService;

        public ApplicationsController(
            IAccountContext accountContext,
            ICommentThreadContext commentThreadContext,
            IRecruitmentService recruitmentService,
            IAssignmentService assignmentService,
            IAccountService accountService,
            INotificationsService notificationsService,
            IDisplayNameService displayNameService,
            ILogger logger
        ) {
            _accountContext = accountContext;
            _commentThreadContext = commentThreadContext;
            _assignmentService = assignmentService;
            _recruitmentService = recruitmentService;
            _accountService = accountService;
            _notificationsService = notificationsService;
            _displayNameService = displayNameService;
            _logger = logger;
        }

        [HttpPost, Authorize, Permissions(Permissions.CONFIRMED)]
        public async Task<IActionResult> Post([FromBody] JObject body) {
            Account account = _accountService.GetUserAccount();
            await Update(body, account);
            CommentThread recruiterCommentThread = new() { Authors = _recruitmentService.GetRecruiterLeads().Values.ToArray(), Mode = ThreadMode.RECRUITER };
            CommentThread applicationCommentThread = new() { Authors = new[] { account.Id }, Mode = ThreadMode.RECRUITER };
            await _commentThreadContext.Add(recruiterCommentThread);
            await _commentThreadContext.Add(applicationCommentThread);
            Application application = new() {
                DateCreated = DateTime.Now,
                State = ApplicationState.WAITING,
                Recruiter = _recruitmentService.GetRecruiter(),
                RecruiterCommentThread = recruiterCommentThread.Id,
                ApplicationCommentThread = applicationCommentThread.Id
            };
            await _accountContext.Update(account.Id, Builders<Account>.Update.Set(x => x.Application, application));
            account = _accountContext.GetSingle(account.Id);
            Notification notification = await _assignmentService.UpdateUnitRankAndRole(account.Id, "", "Applicant", "Candidate", reason: "you were entered into the recruitment process");
            _notificationsService.Add(notification);
            _notificationsService.Add(
                new Notification {
                    Owner = application.Recruiter,
                    Icon = NotificationIcons.APPLICATION,
                    Message = $"You have been assigned {account.Firstname} {account.Lastname}'s application",
                    Link = $"/recruitment/{account.Id}"
                }
            );
            foreach (string id in _recruitmentService.GetRecruiterLeads().Values.Where(x => account.Application.Recruiter != x)) {
                _notificationsService.Add(
                    new Notification {
                        Owner = id,
                        Icon = NotificationIcons.APPLICATION,
                        Message = $"{_displayNameService.GetDisplayName(account.Application.Recruiter)} has been assigned {account.Firstname} {account.Lastname}'s application",
                        Link = $"/recruitment/{account.Id}"
                    }
                );
            }

            _logger.LogAudit($"Application submitted for {account.Id}. Assigned to {_displayNameService.GetDisplayName(account.Application.Recruiter)}");
            return Ok();
        }

        [HttpPost("update"), Authorize, Permissions(Permissions.CONFIRMED)]
        public async Task<IActionResult> PostUpdate([FromBody] JObject body) {
            Account account = _accountService.GetUserAccount();
            await Update(body, account);
            _notificationsService.Add(
                new Notification {
                    Owner = account.Application.Recruiter,
                    Icon = NotificationIcons.APPLICATION,
                    Message = $"{account.Firstname} {account.Lastname} updated their application",
                    Link = $"/recruitment/{account.Id}"
                }
            );
            string difference = account.Changes(_accountContext.GetSingle(account.Id));
            _logger.LogAudit($"Application updated for {account.Id}: {difference}");
            return Ok();
        }

        private async Task Update(JObject body, Account account) {
            await _accountContext.Update(
                account.Id,
                Builders<Account>.Update.Set(x => x.ArmaExperience, body["armaExperience"].ToString())
                                 .Set(x => x.UnitsExperience, body["unitsExperience"].ToString())
                                 .Set(x => x.Background, body["background"].ToString())
                                 .Set(x => x.MilitaryExperience, string.Equals(body["militaryExperience"].ToString(), "true", StringComparison.InvariantCultureIgnoreCase))
                                 .Set(x => x.RolePreferences, body["rolePreferences"].ToObject<List<string>>())
                                 .Set(x => x.Reference, body["reference"].ToString())
            );
        }
    }
}
