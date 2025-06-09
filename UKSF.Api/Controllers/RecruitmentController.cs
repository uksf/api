using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Mappers;
using UKSF.Api.Models.Request;
using UKSF.Api.Queries;

namespace UKSF.Api.Controllers;

[Route("recruitment")]
public class RecruitmentController(
    IAccountContext accountContext,
    IRecruitmentService recruitmentService,
    IDisplayNameService displayNameService,
    INotificationsService notificationsService,
    IHttpContextService httpContextService,
    IVariablesService variablesService,
    IGetCompletedApplicationsPagedQueryHandler getCompletedApplicationsPagedQueryHandler,
    ICompletedApplicationMapper completedApplicationMapper,
    IUksfLogger logger
) : ControllerBase
{
    [HttpGet("applications/completed")]
    [Permissions(Permissions.Recruiter)]
    public async Task<PagedResult<CompletedApplication>> GetCompletedApplicationsPaged(
        [FromQuery] int page,
        [FromQuery] int pageSize = 15,
        [FromQuery] string query = null,
        [FromQuery] ApplicationSortMode sortMode = default,
        [FromQuery] int sortDirection = -1,
        [FromQuery] string recruiterId = default
    )
    {
        var pagedResult = await getCompletedApplicationsPagedQueryHandler.ExecuteAsync(
            new GetCompletedApplicationsPagedQuery(page, pageSize, query, sortMode, sortDirection, recruiterId)
        );

        var completedApplications = pagedResult.Data.Select(completedApplicationMapper.MapToCompletedApplication);
        return new PagedResult<CompletedApplication>(pagedResult.TotalCount, completedApplications);
    }

    [HttpGet("applications/active")]
    [Permissions(Permissions.Recruiter)]
    public List<ActiveApplication> GetActiveApplications()
    {
        return recruitmentService.GetActiveApplications();
    }

    [HttpGet("applications/{accountId}")]
    [Authorize]
    public DetailedApplication GetSingle([FromRoute] string accountId)
    {
        var account = accountContext.GetSingle(accountId);
        return recruitmentService.GetApplication(account);
    }

    [HttpPost("applications/{accountId}")]
    [Permissions(Permissions.Recruiter)]
    public async Task UpdateState(
        [FromServices] IUpdateApplicationCommand updateApplicationCommand,
        [FromRoute] string accountId,
        [FromBody] UpdateApplicationStateRequest updateApplicationStateRequest
    )
    {
        var updatedState = updateApplicationStateRequest.UpdatedState;
        var account = accountContext.GetSingle(accountId);
        if (updatedState == account.Application.State)
        {
            return;
        }

        var age = account.Dob.ToAge();
        var acceptableAge = variablesService.GetVariable("RECRUITMENT_ENTRY_AGE").AsInt();
        if (updatedState == ApplicationState.Accepted && !age.IsAcceptableAge(acceptableAge) && !httpContextService.UserHasPermission(Permissions.Superadmin))
        {
            throw new AgeNotAllowedException();
        }

        await updateApplicationCommand.ExecuteAsync(accountId, updatedState);
    }

    [HttpPost("applications/{accountId}/recruiter")]
    [Permissions(Permissions.RecruiterLead)]
    public async Task PostReassignment([FromRoute] string accountId, [FromBody] AssignRecruiterRequest assignRecruiterRequest)
    {
        if (!httpContextService.UserHasPermission(Permissions.Admin) && !recruitmentService.IsRecruiterLead())
        {
            throw new Exception($"Attempted to assign recruiter to {assignRecruiterRequest.NewRecruiter}. Context is not recruitment lead.");
        }

        await recruitmentService.SetApplicationRecruiter(accountId, assignRecruiterRequest.NewRecruiter);
        var account = accountContext.GetSingle(accountId);
        if (account.Application.State == ApplicationState.Waiting)
        {
            notificationsService.Add(
                new DomainNotification
                {
                    Owner = assignRecruiterRequest.NewRecruiter,
                    Icon = NotificationIcons.Application,
                    Message = $"{account.Firstname} {account.Lastname}'s application has been transferred to you",
                    Link = $"/recruitment/{account.Id}"
                }
            );
        }

        logger.LogAudit($"Application recruiter changed for {accountId} to {assignRecruiterRequest.NewRecruiter}");
    }

    [HttpGet("recruiters")]
    [Permissions(Permissions.Recruiter)]
    public IEnumerable<Recruiter> GetRecruiters()
    {
        return recruitmentService.GetRecruiterAccounts()
                                 .Select(x => new Recruiter
                                     {
                                         Id = x.Id,
                                         Name = displayNameService.GetDisplayName(x),
                                         Active = x.Settings.Sr1Enabled
                                     }
        );
    }

    [HttpGet("stats")]
    [Permissions(Permissions.Recruiter)]
    public RecruitmentStatsDataset GetRecruitmentStats()
    {
        var account = httpContextService.GetUserId();
        var activity = recruitmentService.GetRecruiterAccounts()
                                         .Select(recruiterAccount => new
                                             {
                                                 recruiterAccount,
                                                 recruiterApplications = accountContext
                                                                         .Get(x => x.Application is not null && x.Application.Recruiter == recruiterAccount.Id)
                                                                         .ToList()
                                             }
                                         )
                                         .Select(x => new RecruitmentActivityDataset
                                             {
                                                 Account = new { id = x.recruiterAccount.Id, settings = x.recruiterAccount.Settings },
                                                 Name = displayNameService.GetDisplayName(x.recruiterAccount),
                                                 Active = x.recruiterApplications.Count(y => y.Application.State == ApplicationState.Waiting),
                                                 Accepted = x.recruiterApplications.Count(y => y.Application.State == ApplicationState.Accepted),
                                                 Rejected = x.recruiterApplications.Count(y => y.Application.State == ApplicationState.Rejected)
                                             }
                                         )
                                         .ToList();

        return new RecruitmentStatsDataset
        {
            Activity = activity,
            YourStats =
                new RecruitmentStats { LastMonth = recruitmentService.GetStats(account, true), Overall = recruitmentService.GetStats(account, false) },
            Sr1Stats = new RecruitmentStats { LastMonth = recruitmentService.GetStats("", true), Overall = recruitmentService.GetStats("", false) }
        };
    }
}
