using MongoDB.Driver;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Shared.Commands;

public interface IUpdateApplicationCommand
{
    Task ExecuteAsync(string accountId, ApplicationState updatedState);
}

public class UpdateApplicationCommand : IUpdateApplicationCommand
{
    private readonly IAccountContext _accountContext;
    private readonly IAccountService _accountService;
    private readonly IAssignmentService _assignmentService;
    private readonly INotificationsService _notificationsService;
    private readonly IRecruitmentService _recruitmentService;
    private readonly IHttpContextService _httpContextService;
    private readonly IDisplayNameService _displayNameService;
    private readonly IUksfLogger _logger;

    public UpdateApplicationCommand(
        IAccountContext accountContext,
        IAccountService accountService,
        IAssignmentService assignmentService,
        INotificationsService notificationsService,
        IRecruitmentService recruitmentService,
        IHttpContextService httpContextService,
        IDisplayNameService displayNameService,
        IUksfLogger logger
    )
    {
        _accountContext = accountContext;
        _accountService = accountService;
        _assignmentService = assignmentService;
        _notificationsService = notificationsService;
        _recruitmentService = recruitmentService;
        _httpContextService = httpContextService;
        _displayNameService = displayNameService;
        _logger = logger;
    }

    public async Task ExecuteAsync(string accountId, ApplicationState updatedState)
    {
        var domainAccount = _accountContext.GetSingle(accountId);

        await _accountContext.Update(accountId, Builders<DomainAccount>.Update.Set(x => x.Application.State, updatedState));
        _logger.LogAudit($"Application state changed for {accountId} from {domainAccount.Application.State} to {updatedState}");

        switch (updatedState)
        {
            case ApplicationState.ACCEPTED:
            {
                await _accountContext.Update(
                    accountId,
                    Builders<DomainAccount>.Update.Set(x => x.Application.DateAccepted, DateTime.UtcNow).Set(x => x.MembershipState, MembershipState.MEMBER)
                );
                var notification = await _assignmentService.UpdateUnitRankAndRole(
                    accountId,
                    "Basic Training Unit",
                    "Trainee",
                    "Recruit",
                    reason: "your application was accepted"
                );
                _notificationsService.Add(notification);
                break;
            }
            case ApplicationState.REJECTED:
            {
                await _accountContext.Update(
                    accountId,
                    Builders<DomainAccount>.Update.Set(x => x.Application.DateAccepted, DateTime.UtcNow).Set(x => x.MembershipState, MembershipState.CONFIRMED)
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
                break;
            }
            case ApplicationState.WAITING:
            {
                await _accountContext.Update(
                    accountId,
                    Builders<DomainAccount>.Update.Set(x => x.Application.DateCreated, DateTime.UtcNow)
                                           .Unset(x => x.Application.DateAccepted)
                                           .Set(x => x.MembershipState, MembershipState.CONFIRMED)
                );
                var notification = await _assignmentService.UpdateUnitRankAndRole(
                    accountId,
                    AssignmentService.RemoveFlag,
                    "Applicant",
                    "Candidate",
                    reason: "your application was reactivated"
                );
                _notificationsService.Add(notification);
                if (_recruitmentService.GetRecruiters().All(x => x.Id != domainAccount.Application.Recruiter))
                {
                    var newRecruiterId = _recruitmentService.GetRecruiter();
                    _logger.LogAudit(
                        $"Application recruiter for {accountId} is no longer SR1, reassigning from {domainAccount.Application.Recruiter} to {newRecruiterId}"
                    );
                    await _accountContext.Update(accountId, Builders<DomainAccount>.Update.Set(x => x.Application.Recruiter, newRecruiterId));
                }

                break;
            }
            default: throw new BadRequestException($"New state {updatedState} is invalid");
        }

        var updatedDomainAccount = _accountContext.GetSingle(accountId);
        var message = updatedState == ApplicationState.WAITING ? "was reactivated" : $"was {updatedState}";
        var sessionId = _httpContextService.GetUserId();
        var instigatorName = _httpContextService.GetUserDisplayName();
        if (sessionId != updatedDomainAccount.Application.Recruiter)
        {
            _notificationsService.Add(
                new()
                {
                    Owner = updatedDomainAccount.Application.Recruiter,
                    Icon = NotificationIcons.Application,
                    Message = $"{updatedDomainAccount.Firstname} {updatedDomainAccount.Lastname}'s application {message} by {instigatorName}",
                    Link = $"/recruitment/{accountId}"
                }
            );
        }

        foreach (var value in _recruitmentService.GetRecruiterLeads()
                                                 .Values.Where(value => sessionId != value && updatedDomainAccount.Application.Recruiter != value))
        {
            _notificationsService.Add(
                new()
                {
                    Owner = value,
                    Icon = NotificationIcons.Application,
                    Message = $"{updatedDomainAccount.Firstname} {updatedDomainAccount.Lastname}'s application {message} by {instigatorName}",
                    Link = $"/recruitment/{accountId}"
                }
            );
        }
    }
}
