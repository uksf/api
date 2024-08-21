using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Commands;

public interface IUpdateApplicationCommand
{
    Task ExecuteAsync(string accountId, ApplicationState updatedState);
}

public class UpdateApplicationCommand : IUpdateApplicationCommand
{
    private readonly IAccountContext _accountContext;
    private readonly IAssignmentService _assignmentService;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;
    private readonly IRecruitmentService _recruitmentService;

    public UpdateApplicationCommand(
        IAccountContext accountContext,
        IAssignmentService assignmentService,
        INotificationsService notificationsService,
        IRecruitmentService recruitmentService,
        IHttpContextService httpContextService,
        IUksfLogger logger
    )
    {
        _accountContext = accountContext;
        _assignmentService = assignmentService;
        _notificationsService = notificationsService;
        _recruitmentService = recruitmentService;
        _httpContextService = httpContextService;
        _logger = logger;
    }

    public async Task ExecuteAsync(string accountId, ApplicationState updatedState)
    {
        var account = _accountContext.GetSingle(accountId);

        await _accountContext.Update(accountId, Builders<DomainAccount>.Update.Set(x => x.Application.State, updatedState));
        _logger.LogAudit($"Application state changed for {accountId} from {account.Application.State} to {updatedState}");

        switch (updatedState)
        {
            case ApplicationState.Accepted:
                await Accept(accountId);
                break;
            case ApplicationState.Rejected:
                await Reject(accountId);
                break;
            case ApplicationState.Waiting:
                await Reactivate(accountId, account);
                break;
            default: throw new BadRequestException($"New state {updatedState} is invalid");
        }

        var updatedDomainAccount = _accountContext.GetSingle(accountId);
        var message = updatedState == ApplicationState.Waiting ? "was reactivated" : $"was {updatedState}";
        var sessionId = _httpContextService.GetUserId();
        var instigatorName = _httpContextService.GetUserDisplayName();
        if (sessionId != updatedDomainAccount.Application.Recruiter)
        {
            _notificationsService.Add(
                new DomainNotification
                {
                    Owner = updatedDomainAccount.Application.Recruiter,
                    Icon = NotificationIcons.Application,
                    Message = $"{updatedDomainAccount.Firstname} {updatedDomainAccount.Lastname}'s application {message} by {instigatorName}",
                    Link = $"/recruitment/{accountId}"
                }
            );
        }

        var otherRecruiters = _recruitmentService.GetRecruiterLeads().Values.Where(x => sessionId != x && updatedDomainAccount.Application.Recruiter != x);
        foreach (var value in otherRecruiters)
        {
            _notificationsService.Add(
                new DomainNotification
                {
                    Owner = value,
                    Icon = NotificationIcons.Application,
                    Message = $"{updatedDomainAccount.Firstname} {updatedDomainAccount.Lastname}'s application {message} by {instigatorName}",
                    Link = $"/recruitment/{accountId}"
                }
            );
        }
    }

    private async Task Accept(string accountId)
    {
        await _accountContext.Update(
            accountId,
            Builders<DomainAccount>.Update.Set(x => x.Application.DateAccepted, DateTime.UtcNow).Set(x => x.MembershipState, MembershipState.Member)
        );
        var notification = await _assignmentService.UpdateUnitRankAndRole(
            accountId,
            "Basic Training Unit",
            "Trainee",
            "Recruit",
            reason: "your application was accepted"
        );
        _notificationsService.Add(notification);
    }

    private async Task Reject(string accountId)
    {
        await _accountContext.Update(
            accountId,
            Builders<DomainAccount>.Update.Set(x => x.Application.DateAccepted, DateTime.UtcNow).Set(x => x.MembershipState, MembershipState.Confirmed)
        );
        var notification = await _assignmentService.UpdateUnitRankAndRole(
            accountId,
            AssignmentService.RemoveFlag,
            AssignmentService.RemoveFlag,
            AssignmentService.RemoveFlag,
            "",
            "Unfortunately you have not been accepted into our unit, however we thank you for your interest and hope you find a suitable alternative."
        );
        notification.Link = "/application";
        _notificationsService.Add(notification);
    }

    private async Task Reactivate(string accountId, DomainAccount account)
    {
        await _accountContext.Update(
            accountId,
            Builders<DomainAccount>.Update.Set(x => x.Application.DateCreated, DateTime.UtcNow)
                                   .Unset(x => x.Application.DateAccepted)
                                   .Set(x => x.MembershipState, MembershipState.Confirmed)
        );
        var notification = await _assignmentService.UpdateUnitRankAndRole(
            accountId,
            AssignmentService.RemoveFlag,
            "Applicant",
            "Candidate",
            reason: "your application was reactivated"
        );
        _notificationsService.Add(notification);
        if (_recruitmentService.GetRecruiters().All(x => x.Id != account.Application.Recruiter))
        {
            var newRecruiterId = _recruitmentService.GetRecruiter();
            _logger.LogAudit(
                $"Application recruiter for {accountId} is no longer SR1, reassigning from {account.Application.Recruiter} to {newRecruiterId}"
            );
            await _accountContext.Update(accountId, Builders<DomainAccount>.Update.Set(x => x.Application.Recruiter, newRecruiterId));
        }
    }
}
