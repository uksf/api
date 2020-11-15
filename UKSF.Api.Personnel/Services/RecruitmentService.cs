using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
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
        private readonly IAccountService accountService;
        private readonly IHttpContextService httpContextService;
        private readonly IDisplayNameService displayNameService;
        private readonly IRanksService ranksService;
        private readonly IUnitsService unitsService;
        private readonly IVariablesService variablesService;

        public RecruitmentService(
            IAccountService accountService,
            IHttpContextService httpContextService,
            IDisplayNameService displayNameService,
            IRanksService ranksService,
            IUnitsService unitsService,
            IVariablesService variablesService
        ) {
            this.accountService = accountService;
            this.httpContextService = httpContextService;
            this.displayNameService = displayNameService;
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.variablesService = variablesService;
        }

        public bool IsRecruiter(Account account) => GetRecruiters(true).Any(x => x.id == account.id);

        public Dictionary<string, string> GetRecruiterLeads() => GetRecruiterUnit().roles;

        public IEnumerable<Account> GetRecruiters(bool skipSort = false) {
            IEnumerable<string> members = GetRecruiterUnit().members;
            List<Account> accounts = members.Select(x => accountService.Data.GetSingle(x)).ToList();
            if (skipSort) return accounts;
            return accounts.OrderBy(x => x.rank, new RankComparer(ranksService)).ThenBy(x => x.lastname);
        }

        public object GetAllApplications() {
            JArray waiting = new JArray();
            JArray allWaiting = new JArray();
            JArray complete = new JArray();
            JArray recruiters = new JArray();
            string me = httpContextService.GetUserId();
            IEnumerable<Account> accounts = accountService.Data.Get(x => x.application != null);
            foreach (Account account in accounts) {
                if (account.application.state == ApplicationState.WAITING) {
                    if (account.application.recruiter == me) {
                        waiting.Add(GetWaitingApplication(account));
                    } else {
                        allWaiting.Add(GetWaitingApplication(account));
                    }
                } else {
                    complete.Add(GetCompletedApplication(account));
                }
            }

            foreach (Account account in GetRecruiters(true)) {
                recruiters.Add(displayNameService.GetDisplayName(account));
            }

            return new {waiting, allWaiting, complete, recruiters};
        }

        // TODO: Make sure frontend calls get online user details for ts and discord
        public JObject GetApplication(Account account) {
            Account recruiterAccount = accountService.Data.GetSingle(account.application.recruiter);
            (int years, int months) = account.dob.ToAge();
            return JObject.FromObject(
                new {
                    account,
                    displayName = displayNameService.GetDisplayName(account),
                    age = new {years, months},
                    daysProcessing = Math.Ceiling((DateTime.Now - account.application.dateCreated).TotalDays),
                    daysProcessed = Math.Ceiling((account.application.dateAccepted - account.application.dateCreated).TotalDays),
                    nextCandidateOp = GetNextCandidateOp(),
                    averageProcessingTime = GetAverageProcessingTime(),
                    steamprofile = "http://steamcommunity.com/profiles/" + account.steamname,
                    recruiter = displayNameService.GetDisplayName(recruiterAccount),
                    recruiterId = recruiterAccount.id
                }
            );
        }

        public object GetActiveRecruiters() => GetRecruiters().Where(x => x.settings.sr1Enabled).Select(x => JObject.FromObject(new {value = x.id, viewValue = displayNameService.GetDisplayName(x)}));

        public bool IsRecruiterLead(Account account = null) => account != null ? GetRecruiterUnit().roles.ContainsValue(account.id) : GetRecruiterUnit().roles.ContainsValue(httpContextService.GetUserId());

        public async Task SetRecruiter(string id, string newRecruiter) {
            await accountService.Data.Update(id, Builders<Account>.Update.Set(x => x.application.recruiter, newRecruiter));
        }

        public object GetStats(string account, bool monthly) {
            IEnumerable<Account> accounts = accountService.Data.Get(x => x.application != null);
            if (account != string.Empty) {
                accounts = accounts.Where(x => x.application.recruiter == account);
            }

            if (monthly) {
                accounts = accounts.Where(x => x.application.dateAccepted < DateTime.Now && x.application.dateAccepted > DateTime.Now.AddMonths(-1));
            }

            List<Account> accountsList = accounts.ToList();
            int acceptedApps = accountsList.Count(x => x.application.state == ApplicationState.ACCEPTED);
            int rejectedApps = accountsList.Count(x => x.application.state == ApplicationState.REJECTED);
            int waitingApps = accountsList.Count(x => x.application.state == ApplicationState.WAITING);

            List<Account> processedApplications = accountsList.Where(x => x.application.state != ApplicationState.WAITING).ToList();
            double totalProcessingTime = processedApplications.Sum(x => (x.application.dateAccepted - x.application.dateCreated).TotalDays);
            double averageProcessingTime = totalProcessingTime > 0 ? Math.Round(totalProcessingTime / processedApplications.Count, 1) : 0;
            double enlistmentRate = acceptedApps != 0 || rejectedApps != 0 ? Math.Round((double) acceptedApps / (acceptedApps + rejectedApps) * 100, 1) : 0;

            return new[] {
                new {fieldName = "Accepted applications", fieldValue = acceptedApps.ToString()},
                new {fieldName = "Rejected applications", fieldValue = rejectedApps.ToString()},
                new {fieldName = "Waiting applications", fieldValue = waitingApps.ToString()},
                new {fieldName = "Average processing time", fieldValue = averageProcessingTime + " Days"},
                new {fieldName = "Enlistment Rate", fieldValue = enlistmentRate + "%"}
            };
        }

        public string GetRecruiter() {
            IEnumerable<Account> recruiters = GetRecruiters().Where(x => x.settings.sr1Enabled);
            List<Account> waiting = accountService.Data.Get(x => x.application != null && x.application.state == ApplicationState.WAITING).ToList();
            List<Account> complete = accountService.Data.Get(x => x.application != null && x.application.state != ApplicationState.WAITING).ToList();
            var unsorted = recruiters.Select(x => new {x.id, complete = complete.Count(y => y.application.recruiter == x.id), waiting = waiting.Count(y => y.application.recruiter == x.id)});
            var sorted = unsorted.OrderBy(x => x.waiting).ThenBy(x => x.complete);
            return sorted.First().id;
        }

        private Unit GetRecruiterUnit() {
            string id = variablesService.GetVariable("UNIT_ID_RECRUITMENT").AsString();
            return unitsService.Data.GetSingle(id);
        }

        private JObject GetCompletedApplication(Account account) =>
            JObject.FromObject(
                new {account, displayName = displayNameService.GetDisplayNameWithoutRank(account), daysProcessed = Math.Ceiling((account.application.dateAccepted - account.application.dateCreated).TotalDays), recruiter = displayNameService.GetDisplayName(account.application.recruiter)}
            );

        // TODO: Make sure frontend calls get online user details for ts and discord
        private JObject GetWaitingApplication(Account account) {
            double averageProcessingTime = GetAverageProcessingTime();
            double daysProcessing = Math.Ceiling((DateTime.Now - account.application.dateCreated).TotalDays);
            double processingDifference = daysProcessing - averageProcessingTime;
            return JObject.FromObject(
                new {
                    account,
                    steamprofile = "http://steamcommunity.com/profiles/" + account.steamname,
                    daysProcessing,
                    processingDifference,
                    recruiter = displayNameService.GetDisplayName(account.application.recruiter)
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
            List<Account> waitingApplications = accountService.Data.Get(x => x.application != null && x.application.state != ApplicationState.WAITING).ToList();
            double days = waitingApplications.Sum(x => (x.application.dateAccepted - x.application.dateCreated).TotalDays);
            double time = Math.Round(days / waitingApplications.Count, 1);
            return time;
        }
    }
}
