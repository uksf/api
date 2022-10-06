using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Parameters;
using UKSF.Api.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class RecruitmentController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IAccountService _accountService;
    private readonly IAssignmentService _assignmentService;
    private readonly IDisplayNameService _displayNameService;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;
    private readonly IRecruitmentService _recruitmentService;
    private readonly IVariablesService _variablesService;

    public RecruitmentController(
        IAccountContext accountContext,
        IAccountService accountService,
        IRecruitmentService recruitmentService,
        IAssignmentService assignmentService,
        IDisplayNameService displayNameService,
        INotificationsService notificationsService,
        IHttpContextService httpContextService,
        IVariablesService variablesService,
        IUksfLogger logger
    )
    {
        _accountContext = accountContext;
        _accountService = accountService;
        _recruitmentService = recruitmentService;
        _assignmentService = assignmentService;
        _displayNameService = displayNameService;
        _notificationsService = notificationsService;
        _httpContextService = httpContextService;
        _variablesService = variablesService;
        _logger = logger;
    }

    [HttpGet]
    [Permissions(Permissions.Recruiter)]
    public ApplicationsOverview GetAll()
    {
        return _recruitmentService.GetAllApplications();
    }

    [HttpGet("{id}")]
    [Authorize]
    public DetailedApplication GetSingle([FromRoute] string id)
    {
        var domainAccount = _accountContext.GetSingle(id);
        return _recruitmentService.GetApplication(domainAccount);
    }

    [HttpGet("isrecruiter")]
    [Permissions(Permissions.Recruiter)]
    public bool GetIsRecruiter()
    {
        return _recruitmentService.IsRecruiter(_accountService.GetUserAccount());
    }

    [HttpGet("stats")]
    [Permissions(Permissions.Recruiter)]
    public RecruitmentStatsDataset GetRecruitmentStats()
    {
        var account = _httpContextService.GetUserId();
        List<RecruitmentActivityDataset> activity = new();
        foreach (var recruiterAccount in _recruitmentService.GetRecruiters())
        {
            var recruiterApplications = _accountContext.Get(x => x.Application != null && x.Application.Recruiter == recruiterAccount.Id).ToList();
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

    [HttpPost("{id}")]
    [Permissions(Permissions.Recruiter)]
    public async Task UpdateState([FromRoute] string id, [FromBody] UpdateApplicationStateRequest updateApplicationStateRequest)
    {
        var updatedState = updateApplicationStateRequest.UpdatedState;
        var domainAccount = _accountContext.GetSingle(id);
        if (updatedState == domainAccount.Application.State)
        {
            return;
        }

        var age = domainAccount.Dob.ToAge();
        var acceptableAge = _variablesService.GetVariable("RECRUITMENT_ENTRY_AGE").AsInt();
        if (updatedState == ApplicationState.ACCEPTED && !age.IsAcceptableAge(acceptableAge))
        {
            throw new AgeNotAllowedException();
        }

        var sessionId = _httpContextService.GetUserId();
        await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.Application.State, updatedState));
        _logger.LogAudit($"Application state changed for {id} from {domainAccount.Application.State} to {updatedState}");

        switch (updatedState)
        {
            case ApplicationState.ACCEPTED:
            {
                await _accountContext.Update(
                    id,
                    Builders<DomainAccount>.Update.Set(x => x.Application.DateAccepted, DateTime.UtcNow).Set(x => x.MembershipState, MembershipState.MEMBER)
                );
                var notification = await _assignmentService.UpdateUnitRankAndRole(
                    id,
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
                    id,
                    Builders<DomainAccount>.Update.Set(x => x.Application.DateAccepted, DateTime.UtcNow).Set(x => x.MembershipState, MembershipState.CONFIRMED)
                );
                var notification = await _assignmentService.UpdateUnitRankAndRole(
                    id,
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
                    id,
                    Builders<DomainAccount>.Update.Set(x => x.Application.DateCreated, DateTime.UtcNow)
                                           .Unset(x => x.Application.DateAccepted)
                                           .Set(x => x.MembershipState, MembershipState.CONFIRMED)
                );
                var notification = await _assignmentService.UpdateUnitRankAndRole(
                    id,
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
                        $"Application recruiter for {id} is no longer SR1, reassigning from {domainAccount.Application.Recruiter} to {newRecruiterId}"
                    );
                    await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.Application.Recruiter, newRecruiterId));
                }

                break;
            }
            default: throw new BadRequestException($"New state {updatedState} is invalid");
        }

        domainAccount = _accountContext.GetSingle(id);
        var message = updatedState == ApplicationState.WAITING ? "was reactivated" : $"was {updatedState}";
        if (sessionId != domainAccount.Application.Recruiter)
        {
            _notificationsService.Add(
                new()
                {
                    Owner = domainAccount.Application.Recruiter,
                    Icon = NotificationIcons.Application,
                    Message =
                        $"{domainAccount.Firstname} {domainAccount.Lastname}'s application {message} by {_displayNameService.GetDisplayName(_accountService.GetUserAccount())}",
                    Link = $"/recruitment/{id}"
                }
            );
        }

        foreach (var value in _recruitmentService.GetRecruiterLeads().Values.Where(value => sessionId != value && domainAccount.Application.Recruiter != value))
        {
            _notificationsService.Add(
                new()
                {
                    Owner = value,
                    Icon = NotificationIcons.Application,
                    Message =
                        $"{domainAccount.Firstname} {domainAccount.Lastname}'s application {message} by {_displayNameService.GetDisplayName(_accountService.GetUserAccount())}",
                    Link = $"/recruitment/{id}"
                }
            );
        }
    }

    [HttpPost("recruiter/{id}")]
    [Permissions(Permissions.RecruiterLead)]
    public async Task PostReassignment([FromBody] AssignRecruiterRequest assignRecruiterRequest, [FromRoute] string id)
    {
        if (!_httpContextService.UserHasPermission(Permissions.Admin) && !_recruitmentService.IsRecruiterLead())
        {
            throw new($"attempted to assign recruiter to {assignRecruiterRequest.NewRecruiter}. Context is not recruitment lead.");
        }

        await _recruitmentService.SetRecruiter(id, assignRecruiterRequest.NewRecruiter);
        var domainAccount = _accountContext.GetSingle(id);
        if (domainAccount.Application.State == ApplicationState.WAITING)
        {
            _notificationsService.Add(
                new()
                {
                    Owner = assignRecruiterRequest.NewRecruiter,
                    Icon = NotificationIcons.Application,
                    Message = $"{domainAccount.Firstname} {domainAccount.Lastname}'s application has been transferred to you",
                    Link = $"/recruitment/{domainAccount.Id}"
                }
            );
        }

        _logger.LogAudit($"Application recruiter changed for {id} to {assignRecruiterRequest.NewRecruiter}");
    }

    [HttpPost("ratings/{id}")]
    [Permissions(Permissions.Recruiter)]
    public async Task<Dictionary<string, uint>> Ratings([FromRoute] string id, [FromBody] KeyValuePair<string, uint> value)
    {
        var ratings = _accountContext.GetSingle(id).Application.Ratings;

        var (key, rating) = value;
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

    [HttpGet("recruiters")]
    [Permissions(Permissions.RecruiterLead)]
    public IEnumerable<Recruiter> GetRecruiters()
    {
        return _recruitmentService.GetActiveRecruiters();
    }
}
