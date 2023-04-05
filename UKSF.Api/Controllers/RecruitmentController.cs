using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
public class RecruitmentController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IAccountService _accountService;
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
        var activity = _recruitmentService.GetRecruiters()
                                          .Select(
                                              recruiterAccount => new
                                              {
                                                  recruiterAccount,
                                                  recruiterApplications = _accountContext.Get(
                                                                                             x => x.Application != null &&
                                                                                                  x.Application.Recruiter == recruiterAccount.Id
                                                                                         )
                                                                                         .ToList()
                                              }
                                          )
                                          .Select(
                                              x => new RecruitmentActivityDataset
                                              {
                                                  Account = new { id = x.recruiterAccount.Id, settings = x.recruiterAccount.Settings },
                                                  Name = _displayNameService.GetDisplayName(x.recruiterAccount),
                                                  Active = x.recruiterApplications.Count(y => y.Application.State == ApplicationState.WAITING),
                                                  Accepted = x.recruiterApplications.Count(y => y.Application.State == ApplicationState.ACCEPTED),
                                                  Rejected = x.recruiterApplications.Count(y => y.Application.State == ApplicationState.REJECTED)
                                              }
                                          )
                                          .ToList();

        return new()
        {
            Activity = activity,
            YourStats = new() { LastMonth = _recruitmentService.GetStats(account, true), Overall = _recruitmentService.GetStats(account, false) },
            Sr1Stats = new() { LastMonth = _recruitmentService.GetStats("", true), Overall = _recruitmentService.GetStats("", false) }
        };
    }

    [HttpPost("{id}")]
    [Permissions(Permissions.Recruiter)]
    public async Task UpdateState(
        [FromServices] IUpdateApplicationCommand updateApplicationCommand,
        [FromRoute] string id,
        [FromBody] UpdateApplicationStateRequest updateApplicationStateRequest
    )
    {
        var updatedState = updateApplicationStateRequest.UpdatedState;
        var domainAccount = _accountContext.GetSingle(id);
        if (updatedState == domainAccount.Application.State)
        {
            return;
        }

        var age = domainAccount.Dob.ToAge();
        var acceptableAge = _variablesService.GetVariable("RECRUITMENT_ENTRY_AGE").AsInt();
        if (updatedState == ApplicationState.ACCEPTED && !age.IsAcceptableAge(acceptableAge) && !_httpContextService.UserHasPermission(Permissions.Superadmin))
        {
            throw new AgeNotAllowedException();
        }

        await updateApplicationCommand.ExecuteAsync(id, updatedState);
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
        ratings[key] = rating;

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
