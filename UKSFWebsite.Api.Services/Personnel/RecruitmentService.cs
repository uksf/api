using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Models.Units;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Personnel {
    public class RecruitmentService : IRecruitmentService {
        private readonly IAccountService accountService;
        private readonly IDiscordService discordService;
        private readonly IDisplayNameService displayNameService;
        private readonly ITeamspeakMetricsService metricsService;
        private readonly IRanksService ranksService;
        private readonly ISessionService sessionService;
        private readonly ITeamspeakService teamspeakService;
        private readonly IUnitsService unitsService;

        public RecruitmentService(
            ITeamspeakMetricsService metricsService,
            IAccountService accountService,
            ISessionService sessionService,
            IDisplayNameService displayNameService,
            IDiscordService discordService,
            IRanksService ranksService,
            ITeamspeakService teamspeakService,
            IUnitsService unitsService
        ) {
            this.accountService = accountService;
            this.sessionService = sessionService;
            this.metricsService = metricsService;
            this.displayNameService = displayNameService;
            this.ranksService = ranksService;
            this.teamspeakService = teamspeakService;
            this.unitsService = unitsService;
            this.discordService = discordService;
        }

        public bool IsRecruiter(Account account) => GetSr1Members(true).Any(x => x.id == account.id);

        public Dictionary<string, string> GetSr1Leads() => GetSr1Group().roles;

        public IEnumerable<Account> GetSr1Members(bool skipSort = false) {
            IEnumerable<string> members = unitsService.Data().GetSingle(x => x.name == "SR1 Recruitment").members;
            List<Account> accounts = members.Select(x => accountService.Data().GetSingle(x)).ToList();
            if (skipSort) return accounts;
            return accounts.OrderBy(x => x.rank, new RankComparer(ranksService)).ThenBy(x => x.lastname);
        }

        public object GetAllApplications() {
            JArray waiting = new JArray();
            JArray allWaiting = new JArray();
            JArray complete = new JArray();
            JArray recruiters = new JArray();
            string me = sessionService.GetContextId();
            IEnumerable<Account> accounts = accountService.Data().Get(x => x.application != null);
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

            foreach (Account account in GetSr1Members(true)) {
                recruiters.Add(displayNameService.GetDisplayName(account));
            }

            return new {waiting, allWaiting, complete, recruiters};
        }

        public JObject GetApplication(Account account) {
            Account recruiterAccount = accountService.Data().GetSingle(account.application.recruiter);
            (bool tsOnline, string tsNickname, bool discordOnline) = GetOnlineUserDetails(account);
            (int years, int months) = account.dob.ToAge();
            return JObject.FromObject(
                new {
                    account,
                    displayName = displayNameService.GetDisplayName(account),
                    age = new {years, months},
                    communications = new {tsOnline, tsNickname = tsOnline ? tsNickname : "", discordOnline},
                    daysProcessing = Math.Ceiling((DateTime.Now - account.application.dateCreated).TotalDays),
                    daysProcessed = Math.Ceiling((account.application.dateAccepted - account.application.dateCreated).TotalDays),
                    nextCandidateOp = GetNextCandidateOp(),
                    averageProcessingTime = GetAverageProcessingTime(),
                    teamspeakParticipation = metricsService.GetWeeklyParticipationTrend(account.teamspeakIdentities) + "%",
                    steamprofile = "http://steamcommunity.com/profiles/" + account.steamname,
                    recruiter = displayNameService.GetDisplayName(recruiterAccount),
                    recruiterId = recruiterAccount.id
                }
            );
        }

        public object GetActiveRecruiters() => GetSr1Members().Where(x => x.settings.sr1Enabled).Select(x => JObject.FromObject(new {value = x.id, viewValue = displayNameService.GetDisplayName(x)}));

        public bool IsAccountSr1Lead(Account account = null) => account != null ? GetSr1Group().roles.ContainsValue(account.id) : GetSr1Group().roles.ContainsValue(sessionService.GetContextId());

        public async Task SetRecruiter(string id, string newRecruiter) {
            await accountService.Data().Update(id, Builders<Account>.Update.Set(x => x.application.recruiter, newRecruiter));
        }

        public object GetStats(string account, bool monthly) {
            List<Account> accounts = accountService.Data().Get(x => x.application != null);
            if (account != string.Empty) {
                accounts = accounts.Where(x => x.application.recruiter == account).ToList();
            }

            if (monthly) {
                accounts = accounts.Where(x => x.application.dateAccepted < DateTime.Now && x.application.dateAccepted > DateTime.Now.AddMonths(-1)).ToList();
            }

            int acceptedApps = accounts.Count(x => x.application.state == ApplicationState.ACCEPTED);
            int rejectedApps = accounts.Count(x => x.application.state == ApplicationState.REJECTED);
            int waitingApps = accounts.Count(x => x.application.state == ApplicationState.WAITING);

            List<Account> processedApplications = accounts.Where(x => x.application.state != ApplicationState.WAITING).ToList();
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
            List<Account> recruiters = GetSr1Members().Where(x => x.settings.sr1Enabled).ToList();
            List<Account> waiting = accountService.Data().Get(x => x.application != null && x.application.state == ApplicationState.WAITING);
            List<Account> complete = accountService.Data().Get(x => x.application != null && x.application.state != ApplicationState.WAITING);
            var unsorted = recruiters.Select(x => new {x.id, complete = complete.Count(y => y.application.recruiter == x.id), waiting = waiting.Count(y => y.application.recruiter == x.id)});
            var sorted = unsorted.OrderBy(x => x.waiting).ThenBy(x => x.complete);
            return sorted.First().id;
        }

        private Unit GetSr1Group() {
            return unitsService.Data().Get(x => x.name == "SR1 Recruitment").FirstOrDefault();
        }

        private JObject GetCompletedApplication(Account account) =>
            JObject.FromObject(
                new {account, displayName = displayNameService.GetDisplayNameWithoutRank(account), daysProcessed = Math.Ceiling((account.application.dateAccepted - account.application.dateCreated).TotalDays), recruiter = displayNameService.GetDisplayName(account.application.recruiter)}
            );

        private JObject GetWaitingApplication(Account account) {
            (bool tsOnline, string tsNickname, bool discordOnline) = GetOnlineUserDetails(account);
            double averageProcessingTime = GetAverageProcessingTime();
            double daysProcessing = Math.Ceiling((DateTime.Now - account.application.dateCreated).TotalDays);
            double processingDifference = daysProcessing - averageProcessingTime;
            return JObject.FromObject(
                new {
                    account,
                    communications = new {tsOnline, tsNickname = tsOnline ? tsNickname : "", discordOnline},
                    steamprofile = "http://steamcommunity.com/profiles/" + account.steamname,
                    daysProcessing,
                    processingDifference,
                    recruiter = displayNameService.GetDisplayName(account.application.recruiter)
                }
            );
        }

        private (bool tsOnline, string tsNickname, bool discordOnline) GetOnlineUserDetails(Account account) {
            (bool tsOnline, string tsNickname) = teamspeakService.GetOnlineUserDetails(account);

            return (tsOnline, tsNickname, discordService.IsAccountOnline(account));
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
            List<Account> waitingApplications = accountService.Data().Get(x => x.application != null && x.application.state != ApplicationState.WAITING).ToList();
            double days = waitingApplications.Sum(x => (x.application.dateAccepted - x.application.dateCreated).TotalDays);
            double time = Math.Round(days / waitingApplications.Count, 1);
            return time;
        }
    }
}
