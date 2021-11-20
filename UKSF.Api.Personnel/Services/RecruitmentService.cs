using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Extensions;
using UKSF.Api.Personnel.Mappers;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Services
{
    public interface IRecruitmentService
    {
        ApplicationsOverview GetAllApplications();
        DetailedApplication GetApplication(DomainAccount domainAccount);
        IEnumerable<Recruiter> GetActiveRecruiters();
        IEnumerable<DomainAccount> GetRecruiters(bool skipSort = false);
        Dictionary<string, string> GetRecruiterLeads();
        IEnumerable<RecruitmentStat> GetStats(string account, bool monthly);
        string GetRecruiter();
        bool IsRecruiterLead(DomainAccount domainAccount = null);
        bool IsRecruiter(DomainAccount domainAccount);
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

        public bool IsRecruiter(DomainAccount domainAccount)
        {
            return GetRecruiters(true).Any(x => x.Id == domainAccount.Id);
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
            List<WaitingApplication> waiting = new();
            List<WaitingApplication> allWaiting = new();
            List<CompletedApplication> complete = new();
            var recruiters = GetRecruiters(true).Select(account => _displayNameService.GetDisplayName(account)).ToList();

            var me = _httpContextService.GetUserId();
            var accounts = _accountContext.Get(x => x.Application != null);
            foreach (var account in accounts)
            {
                if (account.Application.State == ApplicationState.WAITING)
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

            return new() { Waiting = waiting, AllWaiting = allWaiting, Complete = complete, Recruiters = recruiters };
        }

        public DetailedApplication GetApplication(DomainAccount domainAccount)
        {
            var recruiterAccount = _accountContext.GetSingle(domainAccount.Application.Recruiter);
            var age = domainAccount.Dob.ToAge();
            return new()
            {
                Account = _accountMapper.MapToAccount(domainAccount),
                DisplayName = _displayNameService.GetDisplayName(domainAccount),
                Age = age,
                DaysProcessing = Math.Ceiling((DateTime.UtcNow - domainAccount.Application.DateCreated).TotalDays),
                DaysProcessed = Math.Ceiling((domainAccount.Application.DateAccepted - domainAccount.Application.DateCreated).TotalDays),
                NextCandidateOp = GetNextCandidateOp(),
                AverageProcessingTime = GetAverageProcessingTime(),
                SteamProfile = "https://steamcommunity.com/profiles/" + domainAccount.Steamname,
                Recruiter = _displayNameService.GetDisplayName(recruiterAccount),
                RecruiterId = recruiterAccount.Id
            };
        }

        public IEnumerable<Recruiter> GetActiveRecruiters()
        {
            return GetRecruiters().Where(x => x.Settings.Sr1Enabled).Select(x => new Recruiter { Id = x.Id, Name = _displayNameService.GetDisplayName(x) });
        }

        public bool IsRecruiterLead(DomainAccount domainAccount = null)
        {
            return domainAccount != null
                ? GetRecruiterUnit().Roles.ContainsValue(domainAccount.Id)
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
            var acceptedApps = accountsList.Count(x => x.Application.State == ApplicationState.ACCEPTED);
            var rejectedApps = accountsList.Count(x => x.Application.State == ApplicationState.REJECTED);
            var waitingApps = accountsList.Count(x => x.Application.State == ApplicationState.WAITING);

            var processedApplications = accountsList.Where(x => x.Application.State != ApplicationState.WAITING).ToList();
            var totalProcessingTime = processedApplications.Sum(x => (x.Application.DateAccepted - x.Application.DateCreated).TotalDays);
            var averageProcessingTime = totalProcessingTime > 0 ? Math.Round(totalProcessingTime / processedApplications.Count, 1) : 0;
            var enlistmentRate = acceptedApps != 0 || rejectedApps != 0 ? Math.Round((double)acceptedApps / (acceptedApps + rejectedApps) * 100, 1) : 0;

            return new RecruitmentStat[]
            {
                new() { FieldName = "Accepted applications", FieldValue = acceptedApps.ToString() },
                new() { FieldName = "Rejected applications", FieldValue = rejectedApps.ToString() },
                new() { FieldName = "Waiting applications", FieldValue = waitingApps.ToString() },
                new() { FieldName = "Average processing time", FieldValue = averageProcessingTime + " Days" },
                new() { FieldName = "Enlistment Rate", FieldValue = enlistmentRate + "%" }
            };
        }

        public string GetRecruiter()
        {
            var recruiters = GetRecruiters().Where(x => x.Settings.Sr1Enabled);
            var waiting = _accountContext.Get(x => x.Application is { State: ApplicationState.WAITING }).ToList();
            var complete = _accountContext.Get(x => x.Application is { State: not ApplicationState.WAITING }).ToList();
            var unsorted = recruiters.Select(
                x => new
                {
                    id = x.Id,
                    complete = complete.Count(y => y.Application.Recruiter == x.Id),
                    waiting = waiting.Count(y => y.Application.Recruiter == x.Id)
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

        private CompletedApplication GetCompletedApplication(DomainAccount domainAccount)
        {
            return new()
            {
                Account = _accountMapper.MapToAccount(domainAccount),
                DisplayName = _displayNameService.GetDisplayNameWithoutRank(domainAccount),
                DaysProcessed = Math.Ceiling((domainAccount.Application.DateAccepted - domainAccount.Application.DateCreated).TotalDays),
                Recruiter = _displayNameService.GetDisplayName(domainAccount.Application.Recruiter)
            };
        }

        private WaitingApplication GetWaitingApplication(DomainAccount domainAccount)
        {
            var averageProcessingTime = GetAverageProcessingTime();
            var daysProcessing = Math.Ceiling((DateTime.UtcNow - domainAccount.Application.DateCreated).TotalDays);
            var processingDifference = daysProcessing - averageProcessingTime;
            return new()
            {
                Account = _accountMapper.MapToAccount(domainAccount),
                SteamProfile = "https://steamcommunity.com/profiles/" + domainAccount.Steamname,
                DaysProcessing = daysProcessing,
                ProcessingDifference = processingDifference,
                Recruiter = _displayNameService.GetDisplayName(domainAccount.Application.Recruiter)
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
            var waitingApplications = _accountContext.Get(x => x.Application != null && x.Application.State != ApplicationState.WAITING).ToList();
            var days = waitingApplications.Sum(x => (x.Application.DateAccepted - x.Application.DateCreated).TotalDays);
            var time = Math.Round(days / waitingApplications.Count, 1);
            return time;
        }
    }
}
