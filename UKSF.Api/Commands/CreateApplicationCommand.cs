using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;

namespace UKSF.Api.Commands;

public interface ICreateApplicationCommand
{
    Task ExecuteAsync(string id, Account account);
}

public class CreateApplicationCommand(
    IAccountContext accountContext,
    IRecruitmentService recruitmentService,
    IAssignmentService assignmentService,
    INotificationsService notificationsService,
    IDisplayNameService displayNameService,
    ICommentThreadService commentThreadService,
    ICreateCommentThreadCommand createCommentThreadCommand,
    IUksfLogger logger
) : ICreateApplicationCommand
{
    private readonly List<MembershipState> _allowedMembershipStates = [MembershipState.Confirmed, MembershipState.Member, MembershipState.Discharged];

    public async Task ExecuteAsync(string id, Account account)
    {
        var application = await CreateApplication(id);
        var updatedAccount = await UpdateDomainAccount(id, account, application);

        await AssignAndNotify(updatedAccount, application);
        await CheckExistingSteamAndDiscordConnections(updatedAccount);

        logger.LogAudit($"Application submitted for {id}. Assigned to {displayNameService.GetDisplayName(updatedAccount.Application.Recruiter)}");
    }

    private async Task<DomainApplication> CreateApplication(string accountId)
    {
        var recruiterCommentThread = await createCommentThreadCommand.ExecuteAsync(
            recruitmentService.GetRecruiterLeadAccountIds().ToArray(),
            ThreadMode.Recruiter
        );
        var applicationCommentThread = await createCommentThreadCommand.ExecuteAsync([accountId], ThreadMode.Recruiter);

        DomainApplication application = new()
        {
            DateCreated = DateTime.UtcNow,
            State = ApplicationState.Waiting,
            Recruiter = recruitmentService.GetNextRecruiterForApplication(),
            RecruiterCommentThread = recruiterCommentThread.Id,
            ApplicationCommentThread = applicationCommentThread.Id
        };

        return application;
    }

    private async Task<DomainAccount> UpdateDomainAccount(string id, Account account, DomainApplication application)
    {
        await accountContext.Update(
            id,
            Builders<DomainAccount>.Update.Set(x => x.ArmaExperience, account.ArmaExperience)
                                   .Set(x => x.UnitsExperience, account.UnitsExperience)
                                   .Set(x => x.Background, account.Background)
                                   .Set(x => x.MilitaryExperience, account.MilitaryExperience)
                                   .Set(x => x.RolePreferences, account.RolePreferences)
                                   .Set(x => x.Reference, account.Reference)
                                   .Set(x => x.Application, application)
        );

        return accountContext.GetSingle(id);
    }

    private async Task AssignAndNotify(DomainAccount account, DomainApplication application)
    {
        var notification = await assignmentService.UpdateUnitRankAndRole(
            account.Id,
            "",
            "Applicant",
            "Candidate",
            reason: "you were entered into the recruitment process"
        );

        notificationsService.Add(notification);
        notificationsService.Add(
            new DomainNotification
            {
                Owner = application.Recruiter,
                Icon = NotificationIcons.Application,
                Message = $"You have been assigned {account.Firstname} {account.Lastname}'s application",
                Link = $"/recruitment/{account.Id}"
            }
        );

        foreach (var id in recruitmentService.GetRecruiterLeadAccountIds().Where(x => account.Application.Recruiter != x))
        {
            notificationsService.Add(
                new DomainNotification
                {
                    Owner = id,
                    Icon = NotificationIcons.Application,
                    Message =
                        $"{displayNameService.GetDisplayName(account.Application.Recruiter)} has been assigned {account.Firstname} {account.Lastname}'s application",
                    Link = $"/recruitment/{account.Id}"
                }
            );
        }
    }

    private async Task CheckExistingSteamAndDiscordConnections(DomainAccount account)
    {
        var accountName = displayNameService.GetDisplayNameWithoutRank(account);
        var accountsWithSameSteamConnection = accountContext
                                              .Get(
                                                  x => x.Id != account.Id &&
                                                       x.Steamname == account.Steamname &&
                                                       _allowedMembershipStates.Contains(x.MembershipState)
                                              )
                                              .Select(displayNameService.GetDisplayNameWithoutRank)
                                              .ToList();
        var accountsWithSameDiscordConnection = accountContext
                                                .Get(
                                                    x => x.Id != account.Id &&
                                                         x.DiscordId == account.DiscordId &&
                                                         _allowedMembershipStates.Contains(x.MembershipState)
                                                )
                                                .Select(displayNameService.GetDisplayNameWithoutRank)
                                                .ToList();

        if (accountsWithSameSteamConnection.Count != 0)
        {
            await commentThreadService.InsertComment(
                account.Application.RecruiterCommentThread,
                new DomainComment
                {
                    Author = ObjectId.Empty.ToString(),
                    Content = $"{accountName} has the same Steam account as {accountsWithSameSteamConnection.Aggregate((a, b) => $"{a}, {b}")}",
                    Timestamp = DateTime.UtcNow
                }
            );
        }

        if (accountsWithSameDiscordConnection.Count != 0)
        {
            await commentThreadService.InsertComment(
                account.Application.RecruiterCommentThread,
                new DomainComment
                {
                    Author = ObjectId.Empty.ToString(),
                    Content = $"{accountName} has the same Discord account as {accountsWithSameDiscordConnection.Aggregate((a, b) => $"{a}, {b}")}",
                    Timestamp = DateTime.UtcNow
                }
            );
        }
    }
}
