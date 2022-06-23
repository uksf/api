using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Commands
{
    public interface ICreateApplicationCommand
    {
        Task ExecuteAsync(string id, Account jObject);
    }

    public class CreateApplicationCommand : ICreateApplicationCommand
    {
        private readonly IAccountContext _accountContext;

        private readonly List<MembershipState> _allowedMembershipStates = new()
        {
            MembershipState.CONFIRMED, MembershipState.MEMBER, MembershipState.DISCHARGED
        };

        private readonly IAssignmentService _assignmentService;
        private readonly ICommentThreadService _commentThreadService;
        private readonly ICreateCommentThreadCommand _createCommentThreadCommand;
        private readonly IDisplayNameService _displayNameService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;
        private readonly IRecruitmentService _recruitmentService;

        public CreateApplicationCommand(
            IAccountContext accountContext,
            IRecruitmentService recruitmentService,
            IAssignmentService assignmentService,
            INotificationsService notificationsService,
            IDisplayNameService displayNameService,
            ICommentThreadService commentThreadService,
            ICreateCommentThreadCommand createCommentThreadCommand,
            ILogger logger
        )
        {
            _accountContext = accountContext;
            _recruitmentService = recruitmentService;
            _assignmentService = assignmentService;
            _notificationsService = notificationsService;
            _displayNameService = displayNameService;
            _commentThreadService = commentThreadService;
            _createCommentThreadCommand = createCommentThreadCommand;
            _logger = logger;
        }

        public async Task ExecuteAsync(string id, Account account)
        {
            var application = await CreateApplication(id);
            var domainAccount = await UpdateDomainAccount(id, account, application);

            await AssignAndNotify(domainAccount, application);
            await CheckExistingSteamAndDiscordConnections(domainAccount);

            _logger.LogAudit($"Application submitted for {id}. Assigned to {_displayNameService.GetDisplayName(domainAccount.Application.Recruiter)}");
        }

        private async Task<Application> CreateApplication(string accountId)
        {
            var recruiterCommentThread = await _createCommentThreadCommand.ExecuteAsync(
                _recruitmentService.GetRecruiterLeads().Values.ToArray(),
                ThreadMode.RECRUITER
            );
            var applicationCommentThread = await _createCommentThreadCommand.ExecuteAsync(new[] { accountId }, ThreadMode.RECRUITER);

            Application application = new()
            {
                DateCreated = DateTime.UtcNow,
                State = ApplicationState.WAITING,
                Recruiter = _recruitmentService.GetRecruiter(),
                RecruiterCommentThread = recruiterCommentThread.Id,
                ApplicationCommentThread = applicationCommentThread.Id
            };

            return application;
        }

        private async Task<DomainAccount> UpdateDomainAccount(string id, Account account, Application application)
        {
            await _accountContext.Update(
                id,
                Builders<DomainAccount>.Update.Set(x => x.ArmaExperience, account.ArmaExperience)
                                       .Set(x => x.UnitsExperience, account.UnitsExperience)
                                       .Set(x => x.Background, account.Background)
                                       .Set(x => x.MilitaryExperience, account.MilitaryExperience)
                                       .Set(x => x.RolePreferences, account.RolePreferences)
                                       .Set(x => x.Reference, account.Reference)
                                       .Set(x => x.Application, application)
            );

            return _accountContext.GetSingle(id);
        }

        private async Task AssignAndNotify(DomainAccount domainAccount, Application application)
        {
            var notification = await _assignmentService.UpdateUnitRankAndRole(
                domainAccount.Id,
                "",
                "Applicant",
                "Candidate",
                reason: "you were entered into the recruitment process"
            );

            _notificationsService.Add(notification);
            _notificationsService.Add(
                new()
                {
                    Owner = application.Recruiter,
                    Icon = NotificationIcons.Application,
                    Message = $"You have been assigned {domainAccount.Firstname} {domainAccount.Lastname}'s application",
                    Link = $"/recruitment/{domainAccount.Id}"
                }
            );

            foreach (var id in _recruitmentService.GetRecruiterLeads().Values.Where(x => domainAccount.Application.Recruiter != x))
            {
                _notificationsService.Add(
                    new()
                    {
                        Owner = id,
                        Icon = NotificationIcons.Application,
                        Message =
                            $"{_displayNameService.GetDisplayName(domainAccount.Application.Recruiter)} has been assigned {domainAccount.Firstname} {domainAccount.Lastname}'s application",
                        Link = $"/recruitment/{domainAccount.Id}"
                    }
                );
            }
        }

        private async Task CheckExistingSteamAndDiscordConnections(DomainAccount domainAccount)
        {
            var accountName = _displayNameService.GetDisplayNameWithoutRank(domainAccount);
            var accountsWithSameSteamConnection = _accountContext
                                                  .Get(
                                                      x => x.Id != domainAccount.Id &&
                                                           x.Steamname == domainAccount.Steamname &&
                                                           _allowedMembershipStates.Contains(x.MembershipState)
                                                  )
                                                  .Select(x => _displayNameService.GetDisplayNameWithoutRank(x))
                                                  .ToList();
            var accountsWithSameDiscordConnection = _accountContext
                                                    .Get(
                                                        x => x.Id != domainAccount.Id &&
                                                             x.DiscordId == domainAccount.DiscordId &&
                                                             _allowedMembershipStates.Contains(x.MembershipState)
                                                    )
                                                    .Select(x => _displayNameService.GetDisplayNameWithoutRank(x))
                                                    .ToList();

            if (accountsWithSameSteamConnection.Any())
            {
                await _commentThreadService.InsertComment(
                    domainAccount.Application.RecruiterCommentThread,
                    new()
                    {
                        Author = ObjectId.Empty.ToString(),
                        Content = $"{accountName} has the same Steam account as {accountsWithSameSteamConnection.Aggregate((a, b) => $"{a}, {b}")}",
                        Timestamp = DateTime.UtcNow
                    }
                );
            }

            if (accountsWithSameDiscordConnection.Any())
            {
                await _commentThreadService.InsertComment(
                    domainAccount.Application.RecruiterCommentThread,
                    new()
                    {
                        Author = ObjectId.Empty.ToString(),
                        Content = $"{accountName} has the same Discord account as {accountsWithSameDiscordConnection.Aggregate((a, b) => $"{a}, {b}")}",
                        Timestamp = DateTime.UtcNow
                    }
                );
            }
        }
    }
}
