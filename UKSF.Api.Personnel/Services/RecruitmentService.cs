using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.Services {
    public interface IRecruitmentService {
        object GetAllApplications();
        JObject GetApplication(Account account);
        object GetActiveRecruiters();
        IEnumerable<Account> GetRecruiters(bool skipSort = false);
        Dictionary<string, string> GetRecruiterLeads();
        object GetStats(string account, bool monthly);
        string GetRecruiter();
        bool IsRecruiterLead(Account account = null);
        bool IsRecruiter(Account account);
        Task SetRecruiter(string id, string newRecruiter);
    }

    public class RecruitmentService : IRecruitmentService {
        private readonly IAccountContext _accountContext;
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
            IVariablesService variablesService
        ) {
            _accountContext = accountContext;
            _unitsContext = unitsContext;
            _httpContextService = httpContextService;
            _displayNameService = displayNameService;
            _ranksService = ranksService;
            _variablesService = variablesService;
        }

        public bool IsRecruiter(Account account) => GetRecruiters(true).Any(x => x.Id == account.Id);

        public Dictionary<string, string> GetRecruiterLeads() => GetRecruiterUnit().Roles;

        public IEnumerable<Account> GetRecruiters(bool skipSort = false) {
            IEnumerable<string> members = GetRecruiterUnit().Members;
            List<Account> accounts = members.Select(x => _accountContext.GetSingle(x)).ToList();
            if (skipSort) return accounts;
            return accounts.OrderBy(x => x.Rank, new RankComparer(_ranksService)).ThenBy(x => x.Lastname);
        }

        public object GetAllApplications() {
            JArray waiting = new();
            JArray allWaiting = new();
            JArray complete = new();
            JArray recruiters = new();
            string me = _httpContextService.GetUserId();
            IEnumerable<Account> accounts = _accountContext.Get(x => x.Application != null);
            foreach (Account account in accounts) {
                if (account.Application.State == ApplicationState.WAITING) {
                    if (account.Application.Recruiter == me) {
                        waiting.Add(GetWaitingApplication(account));
                    } else {
                        allWaiting.Add(GetWaitingApplication(account));
                    }
                } else {
                    complete.Add(GetCompletedApplication(account));
                }
            }

            foreach (Account account in GetRecruiters(true)) {
                recruiters.Add(_displayNameService.GetDisplayName(account));
            }

            return new { waiting, allWaiting, complete, recruiters };
        }

        // TODO: Make sure frontend calls get online user details for ts and discord
        public JObject GetApplication(Account account) {
            Account recruiterAccount = _accountContext.GetSingle(account.Application.Recruiter);
            (int years, int months) = account.Dob.ToAge();
            return JObject.FromObject(
                new {
                    account,
                    displayName = _displayNameService.GetDisplayName(account),
                    age = new { years, months },
                    daysProcessing = Math.Ceiling((DateTime.Now - account.Application.DateCreated).TotalDays),
                    daysProcessed = Math.Ceiling((account.Application.DateAccepted - account.Application.DateCreated).TotalDays),
                    nextCandidateOp = GetNextCandidateOp(),
                    averageProcessingTime = GetAverageProcessingTime(),
                    steamprofile = "http://steamcommunity.com/profiles/" + account.Steamname,
                    recruiter = _displayNameService.GetDisplayName(recruiterAccount),
                    recruiterId = recruiterAccount.Id
                }
            );
        }

        public object GetActiveRecruiters() =>
            GetRecruiters().Where(x => x.Settings.Sr1Enabled).Select(x => JObject.FromObject(new { value = x.Id, viewValue = _displayNameService.GetDisplayName(x) }));

        public bool IsRecruiterLead(Account account = null) =>
            account != null ? GetRecruiterUnit().Roles.ContainsValue(account.Id) : GetRecruiterUnit().Roles.ContainsValue(_httpContextService.GetUserId());

        public async Task SetRecruiter(string id, string newRecruiter) {
            await _accountContext.Update(id, Builders<Account>.Update.Set(x => x.Application.Recruiter, newRecruiter));
        }

        public object GetStats(string account, bool monthly) {
            IEnumerable<Account> accounts = _accountContext.Get(x => x.Application != null);
            if (account != string.Empty) {
                accounts = accounts.Where(x => x.Application.Recruiter == account);
            }

            if (monthly) {
                accounts = accounts.Where(x => x.Application.DateAccepted < DateTime.Now && x.Application.DateAccepted > DateTime.Now.AddMonths(-1));
            }

            List<Account> accountsList = accounts.ToList();
            int acceptedApps = accountsList.Count(x => x.Application.State == ApplicationState.ACCEPTED);
            int rejectedApps = accountsList.Count(x => x.Application.State == ApplicationState.REJECTED);
            int waitingApps = accountsList.Count(x => x.Application.State == ApplicationState.WAITING);

            List<Account> processedApplications = accountsList.Where(x => x.Application.State != ApplicationState.WAITING).ToList();
            double totalProcessingTime = processedApplications.Sum(x => (x.Application.DateAccepted - x.Application.DateCreated).TotalDays);
            double averageProcessingTime = totalProcessingTime > 0 ? Math.Round(totalProcessingTime / processedApplications.Count, 1) : 0;
            double enlistmentRate = acceptedApps != 0 || rejectedApps != 0 ? Math.Round((double) acceptedApps / (acceptedApps + rejectedApps) * 100, 1) : 0;

            return new[] {
                new { fieldName = "Accepted applications", fieldValue = acceptedApps.ToString() },
                new { fieldName = "Rejected applications", fieldValue = rejectedApps.ToString() },
                new { fieldName = "Waiting applications", fieldValue = waitingApps.ToString() },
                new { fieldName = "Average processing time", fieldValue = averageProcessingTime + " Days" },
                new { fieldName = "Enlistment Rate", fieldValue = enlistmentRate + "%" }
            };
        }

        public string GetRecruiter() {
            IEnumerable<Account> recruiters = GetRecruiters().Where(x => x.Settings.Sr1Enabled);
            List<Account> waiting = _accountContext.Get(x => x.Application != null && x.Application.State == ApplicationState.WAITING).ToList();
            List<Account> complete = _accountContext.Get(x => x.Application != null && x.Application.State != ApplicationState.WAITING).ToList();
            var unsorted = recruiters.Select(x => new { id = x.Id, complete = complete.Count(y => y.Application.Recruiter == x.Id), waiting = waiting.Count(y => y.Application.Recruiter == x.Id) });
            var sorted = unsorted.OrderBy(x => x.waiting).ThenBy(x => x.complete);
            return sorted.First().id;
        }

        private Unit GetRecruiterUnit() {
            string id = _variablesService.GetVariable("UNIT_ID_RECRUITMENT").AsString();
            return _unitsContext.GetSingle(id);
        }

        private JObject GetCompletedApplication(Account account) =>
            JObject.FromObject(
                new {
                    account,
                    displayName = _displayNameService.GetDisplayNameWithoutRank(account),
                    daysProcessed = Math.Ceiling((account.Application.DateAccepted - account.Application.DateCreated).TotalDays),
                    recruiter = _displayNameService.GetDisplayName(account.Application.Recruiter)
                }
            );

        // TODO: Make sure frontend calls get online user details for ts and discord
        private JObject GetWaitingApplication(Account account) {
            double averageProcessingTime = GetAverageProcessingTime();
            double daysProcessing = Math.Ceiling((DateTime.Now - account.Application.DateCreated).TotalDays);
            double processingDifference = daysProcessing - averageProcessingTime;
            return JObject.FromObject(
                new {
                    account,
                    steamprofile = "http://steamcommunity.com/profiles/" + account.Steamname,
                    daysProcessing,
                    processingDifference,
                    recruiter = _displayNameService.GetDisplayName(account.Application.Recruiter)
                }
            );
        }

        private static string GetNextCandidateOp() {
            DateTime nextDate = DateTime.Now;
            while (nextDate.DayOfWeek == DayOfWeek.Monday || nextDate.DayOfWeek == DayOfWeek.Wednesday || nextDate.DayOfWeek == DayOfWeek.Saturday) {
                nextDate = nextDate.AddDays(1);
            }

            if (nextDate.Hour > 18) {
                nextDate = nextDate.AddDays(1);
            }

            return nextDate.Day == DateTime.Today.Day ? "Today" : nextDate.Day == DateTime.Today.AddDays(1).Day ? "Tomorrow" : nextDate.ToString("dddd");
        }

        private double GetAverageProcessingTime() {
            List<Account> waitingApplications = _accountContext.Get(x => x.Application != null && x.Application.State != ApplicationState.WAITING).ToList();
            double days = waitingApplications.Sum(x => (x.Application.DateAccepted - x.Application.DateCreated).TotalDays);
            double time = Math.Round(days / waitingApplications.Count, 1);
            return time;
        }
    }
}
