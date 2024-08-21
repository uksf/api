using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IRecruitmentService
{
    ApplicationsOverview GetAllApplications();
    DetailedApplication GetApplication(DomainAccount account);
    IEnumerable<Recruiter> GetActiveRecruiters();
    IEnumerable<DomainAccount> GetRecruiters(bool skipSort = false);
    Dictionary<string, string> GetRecruiterLeads();
    IEnumerable<RecruitmentStat> GetStats(string account, bool monthly);
    string GetRecruiter();
    bool IsRecruiterLead(DomainAccount account = null);
    bool IsRecruiter(DomainAccount account);
    Task SetRecruiter(string id, string newRecruiter);
}

public class RecruitmentService : IRecruitmentService
{
    private readonly IAccountContext _accountContext;
    private readonly IAccountMapper _accountMapper;
    private readonly IDisplayNameService _displayNameService;
    private readonly IHttpContextService _httpContextService;
    private readonly IRanksService _ranksService;
    private readonly IUnitsContext _unitsContext;
    private readonly IVariablesService _variablesService;

    public RecruitmentService(
        IAccountContext accountContext,
        IUnitsContext unitsContext,
        IHttpContextService httpContextService,
        IDisplayNameService displayNameService,
        IRanksService ranksService,
        IVariablesService variablesService,
        IAccountMapper accountMapper
    )
    {
        _accountContext = accountContext;
        _unitsContext = unitsContext;
        _httpContextService = httpContextService;
        _displayNameService = displayNameService;
        _ranksService = ranksService;
        _variablesService = variablesService;
        _accountMapper = accountMapper;
    }

    public bool IsRecruiter(DomainAccount account)
    {
        return GetRecruiters(true).Any(x => x.Id == account.Id);
    }

    public Dictionary<string, string> GetRecruiterLeads()
    {
        return GetRecruiterUnit().Roles;
    }

    public IEnumerable<DomainAccount> GetRecruiters(bool skipSort = false)
    {
        IEnumerable<string> members = GetRecruiterUnit().Members;
        var accounts = members.Select(x => _accountContext.GetSingle(x)).ToList();
        if (skipSort)
        {
            return accounts;
        }

        return accounts.OrderBy(x => x.Rank, new RankComparer(_ranksService)).ThenBy(x => x.Lastname);
    }

    public ApplicationsOverview GetAllApplications()
    {
        List<WaitingApplication> waiting = [];
        List<WaitingApplication> allWaiting = [];
        List<CompletedApplication> complete = [];
        var recruiters = GetRecruiters(true).Select(account => _displayNameService.GetDisplayName(account)).ToList();

        var me = _httpContextService.GetUserId();
        var accounts = _accountContext.Get(x => x.Application != null);
        foreach (var account in accounts)
        {
            if (account.Application.State == ApplicationState.Waiting)
            {
                if (account.Application.Recruiter == me)
                {
                    waiting.Add(GetWaitingApplication(account));
                }
                else
                {
                    allWaiting.Add(GetWaitingApplication(account));
                }
            }
            else
            {
                complete.Add(GetCompletedApplication(account));
            }
        }

        return new ApplicationsOverview
        {
            Waiting = waiting,
            AllWaiting = allWaiting,
            Complete = complete,
            Recruiters = recruiters
        };
    }

    public DetailedApplication GetApplication(DomainAccount account)
    {
        var recruiterAccount = _accountContext.GetSingle(account.Application.Recruiter);
        var age = account.Dob.ToAge();
        var acceptableAge = _variablesService.GetVariable("RECRUITMENT_ENTRY_AGE");

        return new DetailedApplication
        {
            Account = _accountMapper.MapToAccount(account),
            DisplayName = _displayNameService.GetDisplayName(account),
            Age = age,
            AcceptableAge = acceptableAge.AsInt(),
            DaysProcessing = Math.Ceiling((DateTime.UtcNow - account.Application.DateCreated).TotalDays),
            DaysProcessed = Math.Ceiling((account.Application.DateAccepted - account.Application.DateCreated).TotalDays),
            NextCandidateOp = GetNextCandidateOp(),
            AverageProcessingTime = GetAverageProcessingTime(),
            SteamProfile = "https://steamcommunity.com/profiles/" + account.Steamname,
            Recruiter = _displayNameService.GetDisplayName(recruiterAccount),
            RecruiterId = recruiterAccount.Id
        };
    }

    public IEnumerable<Recruiter> GetActiveRecruiters()
    {
        return GetRecruiters().Where(x => x.Settings.Sr1Enabled).Select(x => new Recruiter { Id = x.Id, Name = _displayNameService.GetDisplayName(x) });
    }

    public bool IsRecruiterLead(DomainAccount account = null)
    {
        return account != null
            ? GetRecruiterUnit().Roles.ContainsValue(account.Id)
            : GetRecruiterUnit().Roles.ContainsValue(_httpContextService.GetUserId());
    }

    public async Task SetRecruiter(string id, string newRecruiter)
    {
        await _accountContext.Update(id, Builders<DomainAccount>.Update.Set(x => x.Application.Recruiter, newRecruiter));
    }

    public IEnumerable<RecruitmentStat> GetStats(string account, bool monthly)
    {
        var accounts = _accountContext.Get(x => x.Application != null);
        if (account != string.Empty)
        {
            accounts = accounts.Where(x => x.Application.Recruiter == account);
        }

        if (monthly)
        {
            accounts = accounts.Where(x => x.Application.DateAccepted < DateTime.UtcNow && x.Application.DateAccepted > DateTime.UtcNow.AddMonths(-1));
        }

        var accountsList = accounts.ToList();
        var acceptedApps = accountsList.Count(x => x.Application.State == ApplicationState.Accepted);
        var rejectedApps = accountsList.Count(x => x.Application.State == ApplicationState.Rejected);
        var waitingApps = accountsList.Count(x => x.Application.State == ApplicationState.Waiting);

        var processedApplications = accountsList.Where(x => x.Application.State != ApplicationState.Waiting).ToList();
        var totalProcessingTime = processedApplications.Sum(x => (x.Application.DateAccepted - x.Application.DateCreated).TotalDays);
        var averageProcessingTime = totalProcessingTime > 0 ? Math.Round(totalProcessingTime / processedApplications.Count, 1) : 0;
        var enlistmentRate = acceptedApps != 0 || rejectedApps != 0 ? Math.Round((double)acceptedApps / (acceptedApps + rejectedApps) * 100, 1) : 0;

        return
        [
            new() { FieldName = "Accepted applications", FieldValue = acceptedApps.ToString() },
            new() { FieldName = "Rejected applications", FieldValue = rejectedApps.ToString() },
            new() { FieldName = "Waiting applications", FieldValue = waitingApps.ToString() },
            new() { FieldName = "Average processing time", FieldValue = averageProcessingTime + " Days" },
            new() { FieldName = "Enlistment Rate", FieldValue = enlistmentRate + "%" }
        ];
    }

    public string GetRecruiter()
    {
        var recruiters = GetRecruiters().Where(x => x.Settings.Sr1Enabled);
        var waiting = _accountContext.Get(x => x.Application is { State: ApplicationState.Waiting }).ToList();
        var complete = _accountContext.Get(x => x.Application is { State: not ApplicationState.Waiting }).ToList();
        var unsorted = recruiters.Select(
            x => new
            {
                id = x.Id, complete = complete.Count(y => y.Application.Recruiter == x.Id), waiting = waiting.Count(y => y.Application.Recruiter == x.Id)
            }
        );
        var sorted = unsorted.OrderBy(x => x.waiting).ThenBy(x => x.complete);
        return sorted.First().id;
    }

    private DomainUnit GetRecruiterUnit()
    {
        var id = _variablesService.GetVariable("UNIT_ID_RECRUITMENT").AsString();
        return _unitsContext.GetSingle(id);
    }

    private CompletedApplication GetCompletedApplication(DomainAccount account)
    {
        return new CompletedApplication
        {
            Account = _accountMapper.MapToAccount(account),
            DisplayName = _displayNameService.GetDisplayNameWithoutRank(account),
            DaysProcessed = Math.Ceiling((account.Application.DateAccepted - account.Application.DateCreated).TotalDays),
            Recruiter = _displayNameService.GetDisplayName(account.Application.Recruiter)
        };
    }

    private WaitingApplication GetWaitingApplication(DomainAccount account)
    {
        var averageProcessingTime = GetAverageProcessingTime();
        var daysProcessing = Math.Ceiling((DateTime.UtcNow - account.Application.DateCreated).TotalDays);
        var processingDifference = daysProcessing - averageProcessingTime;
        return new WaitingApplication
        {
            Account = _accountMapper.MapToAccount(account),
            SteamProfile = "https://steamcommunity.com/profiles/" + account.Steamname,
            DaysProcessing = daysProcessing,
            ProcessingDifference = processingDifference,
            Recruiter = _displayNameService.GetDisplayName(account.Application.Recruiter)
        };
    }

    private static string GetNextCandidateOp()
    {
        var nextDate = DateTime.UtcNow;
        while (nextDate.DayOfWeek != DayOfWeek.Tuesday &&
               nextDate.DayOfWeek != DayOfWeek.Thursday &&
               nextDate.DayOfWeek != DayOfWeek.Friday &&
               nextDate.Hour > 18)
        {
            nextDate = nextDate.AddDays(1);
        }

        return nextDate.Day == DateTime.UtcNow.Date.Day         ? "Today" :
            nextDate.Day == DateTime.UtcNow.Date.AddDays(1).Day ? "Tomorrow" : nextDate.ToString("dddd");
    }

    private double GetAverageProcessingTime()
    {
        var waitingApplications = _accountContext.Get(x => x.Application != null && x.Application.State != ApplicationState.Waiting).ToList();
        var days = waitingApplications.Sum(x => (x.Application.DateAccepted - x.Application.DateCreated).TotalDays);
        var time = Math.Round(days / waitingApplications.Count, 1);
        return time;
    }
}
