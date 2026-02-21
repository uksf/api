using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IRecruitmentService
{
    List<ActiveApplication> GetActiveApplications();
    DetailedApplication GetApplication(DomainAccount account);
    IEnumerable<DomainAccount> GetRecruiterAccounts(bool skipSortByRank = false);
    List<string> GetRecruiterLeadAccountIds();
    bool IsRecruiter(DomainAccount account);
    bool IsRecruiterLead(DomainAccount account = null);
    string GetNextRecruiterForApplication();
    Task SetApplicationRecruiter(string id, string newRecruiter);
    IEnumerable<RecruitmentStat> GetStats(string account, bool monthly);
}

public class RecruitmentService(
    IAccountContext accountContext,
    IUnitsContext unitsContext,
    IHttpContextService httpContextService,
    IDisplayNameService displayNameService,
    IRanksService ranksService,
    IVariablesService variablesService,
    IAccountMapper accountMapper
) : IRecruitmentService
{
    public List<ActiveApplication> GetActiveApplications()
    {
        var averageProcessingTime = GetAverageProcessingTime();

        return accountContext.Get(x => x.Application is not null && x.Application.State == ApplicationState.Waiting)
                             .Select(x => MapToActiveApplication(x, averageProcessingTime))
                             .ToList();
    }

    public DetailedApplication GetApplication(DomainAccount account)
    {
        var recruiterAccount = accountContext.GetSingle(account.Application.Recruiter);
        var age = account.Dob.ToAge();
        var acceptableAge = variablesService.GetVariable("RECRUITMENT_ENTRY_AGE");

        return new DetailedApplication
        {
            Account = accountMapper.MapToAccount(account),
            DisplayName = displayNameService.GetDisplayName(account),
            Age = age,
            AcceptableAge = acceptableAge.AsInt(),
            DaysProcessing = Math.Ceiling((DateTime.UtcNow - account.Application.DateCreated).TotalDays),
            DaysProcessed = Math.Ceiling((account.Application.DateAccepted - account.Application.DateCreated).TotalDays),
            NextCandidateOp = GetNextCandidateOp(),
            AverageProcessingTime = GetAverageProcessingTime(),
            SteamProfile = "https://steamcommunity.com/profiles/" + account.Steamname,
            Recruiter = recruiterAccount != null ? displayNameService.GetDisplayName(recruiterAccount) : string.Empty,
            RecruiterId = recruiterAccount?.Id ?? string.Empty
        };
    }

    public IEnumerable<DomainAccount> GetRecruiterAccounts(bool skipSortByRank = false)
    {
        IEnumerable<string> members = GetRecruiterUnit().Members;
        var accounts = members.Select(accountContext.GetSingle).ToList();
        if (skipSortByRank)
        {
            return accounts;
        }

        return accounts.OrderBy(x => x.Rank, new RankComparer(ranksService)).ThenBy(x => x.Lastname).ThenBy(x => x.Firstname);
    }

    public List<string> GetRecruiterLeadAccountIds()
    {
        var recruiterUnit = GetRecruiterUnit();
        var chainOfCommand = recruiterUnit.ChainOfCommand;
        var leadIds = new List<string>();

        if (!string.IsNullOrEmpty(chainOfCommand?.First))
        {
            leadIds.Add(chainOfCommand.First);
        }

        if (!string.IsNullOrEmpty(chainOfCommand?.Second))
        {
            leadIds.Add(chainOfCommand.Second);
        }

        if (!string.IsNullOrEmpty(chainOfCommand?.Third))
        {
            leadIds.Add(chainOfCommand.Third);
        }

        if (!string.IsNullOrEmpty(chainOfCommand?.Nco))
        {
            leadIds.Add(chainOfCommand.Nco);
        }

        return leadIds;
    }

    public bool IsRecruiter(DomainAccount account)
    {
        return GetRecruiterAccounts(true).Any(x => x.Id == account.Id);
    }

    public bool IsRecruiterLead(DomainAccount account = null)
    {
        var accountId = account?.Id ?? httpContextService.GetUserId();
        return GetRecruiterLeadAccountIds().Contains(accountId);
    }

    public string GetNextRecruiterForApplication()
    {
        var recruiters = GetRecruiterAccounts().Where(x => x.Settings.Sr1Enabled);
        var waiting = accountContext.Get(x => x.Application is { State: ApplicationState.Waiting }).ToList();
        var complete = accountContext.Get(x => x.Application is { State: not ApplicationState.Waiting }).ToList();
        var unsorted = recruiters.Select(x => new
            {
                id = x.Id,
                complete = complete.Count(y => y.Application.Recruiter == x.Id),
                waiting = waiting.Count(y => y.Application.Recruiter == x.Id)
            }
        );
        var sorted = unsorted.OrderBy(x => x.waiting).ThenBy(x => x.complete);
        var next = sorted.FirstOrDefault();
        return next?.id ?? string.Empty;
    }

    public async Task SetApplicationRecruiter(string id, string newRecruiter)
    {
        await accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.Application.Recruiter, newRecruiter));
    }

    public IEnumerable<RecruitmentStat> GetStats(string account, bool monthly)
    {
        var accounts = accountContext.Get(x => x.Application is not null);
        if (account != string.Empty)
        {
            accounts = accounts.Where(x => x.Application.Recruiter == account);
        }

        if (monthly)
        {
            accounts = accounts.Where(x => x.Application.DateAccepted < DateTime.UtcNow && x.Application.DateAccepted > DateTime.UtcNow.AddMonths(-1));
        }

        var accountsList = accounts.ToList();
        var acceptedApps = accountsList.Count(x => x.Application.State is ApplicationState.Accepted);
        var rejectedApps = accountsList.Count(x => x.Application.State is ApplicationState.Rejected);
        var waitingApps = accountsList.Count(x => x.Application.State is ApplicationState.Waiting);

        var processedApplications = accountsList.Where(x => x.Application.State != ApplicationState.Waiting).ToList();
        var totalProcessingTime = processedApplications.Sum(x => (x.Application.DateAccepted - x.Application.DateCreated).TotalDays);
        var averageProcessingTime = totalProcessingTime > 0 ? Math.Round(totalProcessingTime / processedApplications.Count, 1) : 0;
        var enlistmentRate = acceptedApps != 0 || rejectedApps != 0 ? Math.Round((double)acceptedApps / (acceptedApps + rejectedApps) * 100, 1) : 0;

        return
        [
            new RecruitmentStat { FieldName = "Accepted applications", FieldValue = acceptedApps.ToString() },
            new RecruitmentStat { FieldName = "Rejected applications", FieldValue = rejectedApps.ToString() },
            new RecruitmentStat { FieldName = "Waiting applications", FieldValue = waitingApps.ToString() },
            new RecruitmentStat { FieldName = "Average processing time", FieldValue = averageProcessingTime + " Days" },
            new RecruitmentStat { FieldName = "Enlistment Rate", FieldValue = enlistmentRate + "%" }
        ];
    }

    private DomainUnit GetRecruiterUnit()
    {
        var id = variablesService.GetVariable("UNIT_ID_RECRUITMENT").AsString();
        return unitsContext.GetSingle(id);
    }

    private ActiveApplication MapToActiveApplication(DomainAccount account, double averageProcessingTime)
    {
        var daysProcessing = Math.Ceiling((DateTime.UtcNow - account.Application.DateCreated).TotalDays);
        var processingDifference = daysProcessing - averageProcessingTime;
        return new ActiveApplication
        {
            Account = accountMapper.MapToAccount(account),
            SteamProfile = "https://steamcommunity.com/profiles/" + account.Steamname,
            DaysProcessing = daysProcessing,
            ProcessingDifference = processingDifference,
            Recruiter = displayNameService.GetDisplayName(account.Application.Recruiter)
        };
    }

    private static string GetNextCandidateOp()
    {
        var now = DateTime.UtcNow;
        var nextDate = now;
        if (nextDate.Hour > 18)
        {
            nextDate = nextDate.Date.AddDays(1);
        }

        while (nextDate.DayOfWeek != DayOfWeek.Tuesday && nextDate.DayOfWeek != DayOfWeek.Thursday && nextDate.DayOfWeek != DayOfWeek.Friday)
        {
            nextDate = nextDate.AddDays(1);
        }

        return nextDate.Day == now.Date.Day         ? "Today" :
            nextDate.Day == now.Date.AddDays(1).Day ? "Tomorrow" : nextDate.ToString("dddd");
    }

    private double GetAverageProcessingTime()
    {
        var processedApplications = accountContext.Get(x => x.Application is not null && x.Application.State != ApplicationState.Waiting).ToList();
        if (processedApplications.Count == 0)
        {
            return 0;
        }

        var days = processedApplications.Sum(x => (x.Application.DateAccepted - x.Application.DateCreated).TotalDays);
        return Math.Round(days / processedApplications.Count, 1);
    }
}
