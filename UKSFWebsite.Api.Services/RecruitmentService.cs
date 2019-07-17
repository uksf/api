using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;

namespace UKSFWebsite.Api.Services {
    public class RecruitmentService : IRecruitmentService {
        private readonly IAccountService accountService;
        private readonly IDisplayNameService displayNameService;
        private readonly ITeamspeakMetricsService metricsService;
        private readonly INotificationsService notificationsService;
        private readonly IRanksService ranksService;
        private readonly ISessionService sessionService;
        private readonly ITeamspeakService teamspeakService;
        private readonly IUnitsService unitsService;

        public RecruitmentService(
            ITeamspeakMetricsService metricsService,
            IAccountService accountService,
            ISessionService sessionService,
            IDisplayNameService displayNameService,
            IRanksService ranksService,
            INotificationsService notificationsService,
            ITeamspeakService teamspeakService,
            IUnitsService unitsService
        ) {
            this.accountService = accountService;
            this.sessionService = sessionService;
            this.metricsService = metricsService;
            this.displayNameService = displayNameService;
            this.ranksService = ranksService;
            this.notificationsService = notificationsService;
            this.teamspeakService = teamspeakService;
            this.unitsService = unitsService;
        }

        public bool IsRecruiter(Account account) => GetSr1Members(true).Any(x => x.id == account.id);

        public Dictionary<string, string> GetSr1Leads() => GetSr1Group().roles;

        public IEnumerable<Account> GetSr1Members(bool skipSort = false) {
            IEnumerable<string> members = unitsService.GetSingle(x => x.name == "SR1 Recruitment").members;
            List<Account> accounts = members.Select(x => accountService.GetSingle(x)).ToList();
            if (skipSort) return accounts;
            return accounts.OrderBy(x => x.rank, new RankComparer(ranksService)).ThenBy(x => x.lastname);
        }

        public object GetAllApplications() {
            JArray waiting = new JArray();
            JArray allWaiting = new JArray();
            JArray complete = new JArray();
            JArray recruiters = new JArray();
            string me = sessionService.GetContextId();
            IEnumerable<Account> accounts = accountService.Get(x => x.application != null);
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
            Account recruiterAccount = accountService.GetSingle(account.application.recruiter);
            (bool online, string nickname) = GetOnlineUserDetails(account);
            return JObject.FromObject(
                new {
                    account,
                    displayName = displayNameService.GetDisplayNameWithoutRank(account),
                    teamspeak = new {online, nickname = online ? nickname : ""},
                    daysProcessing = Math.Ceiling((DateTime.Now - account.application.dateCreated).TotalDays),
                    daysProcessed = Math.Ceiling((account.application.dateAccepted - account.application.dateCreated).TotalDays),
                    nextCandidateOp = GetNextCandidateOp(),
                    averageProcessingTime = GetAverageProcessingTime(),
                    teamspeakParticipation = metricsService.GetWeeklyParticipationTrend(account.teamspeakIdentities) + "%",
                    steamprofile = "http://steamcommunity.com/profiles/" + account.steamname,
                    recruiter = displayNameService.GetDisplayName(recruiterAccount)
                }
            );
        }

        public object GetOtherRecruiters(string recruiterId) {
            JArray recruitersJArray = new JArray();
            foreach (Account recruiter in GetSr1Members().Where(x => x.settings.sr1Enabled)) {
                if (recruiter.id != recruiterId) {
                    recruitersJArray.Add(JObject.FromObject(new {value = recruiter.id, viewValue = displayNameService.GetDisplayName(recruiter)}));
                }
            }

            return recruitersJArray;
        }

        public bool IsAccountSr1Lead(Account account = null) => account != null ? GetSr1Group().roles.ContainsValue(account.id) : GetSr1Group().roles.ContainsValue(sessionService.GetContextId());

        public async Task SetRecruiter(string id, string newRecruiter) {
            await accountService.Update(id, Builders<Account>.Update.Set(x => x.application.recruiter, newRecruiter));
            Account account = accountService.GetSingle(id);
            if (account.application.state == ApplicationState.WAITING) {
                notificationsService.Add(new Notification {owner = newRecruiter, icon = NotificationIcons.APPLICATION, message = $"{account.firstname} {account.lastname}'s application has been transferred to you", link = $"/recruitment/{account.id}"});
            }
        }

        public object GetStats(string account, bool monthly) {
            List<Account> accounts = accountService.Get(x => x.application != null);
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
            List<Account> waiting = accountService.Get(x => x.application != null && x.application.state == ApplicationState.WAITING);
            List<Account> complete = accountService.Get(x => x.application != null && x.application.state != ApplicationState.WAITING);
            var unsorted = recruiters.Select(x => new {x.id, complete = complete.Count(y => y.application.recruiter == x.id), waiting = waiting.Count(y => y.application.recruiter == x.id)});
            var sorted = unsorted.OrderBy(x => x.waiting).ThenBy(x => x.complete);
            return sorted.First().id;
        }

        public bool IsContextRecruiter() => GetSr1Members(true).Any(x => x.id == sessionService.GetContextId());

        public Unit GetSr1Group() {
            return unitsService.Get(x => x.name == "SR1 Recruitment").FirstOrDefault();
        }

        private JObject GetCompletedApplication(Account account) =>
            JObject.FromObject(
                new {account, displayName = displayNameService.GetDisplayNameWithoutRank(account), daysProcessed = Math.Ceiling((account.application.dateAccepted - account.application.dateCreated).TotalDays), recruiter = displayNameService.GetDisplayName(account.application.recruiter)}
            );

        private JObject GetWaitingApplication(Account account) {
            (bool online, string nickname) = GetOnlineUserDetails(account);
            double averageProcessingTime = GetAverageProcessingTime();
            double daysProcessing = Math.Ceiling((DateTime.Now - account.application.dateCreated).TotalDays);
            double processingDifference = daysProcessing - averageProcessingTime;
            return JObject.FromObject(
                new {
                    account,
                    teamspeak = new {online, nickname = online ? nickname : ""},
                    steamprofile = "http://steamcommunity.com/profiles/" + account.steamname,
                    daysProcessing,
                    processingDifference,
                    recruiter = displayNameService.GetDisplayName(account.application.recruiter)
                }
            );
        }

        private Tuple<bool, string> GetOnlineUserDetails(Account account) {
            if (account.teamspeakIdentities == null) return new Tuple<bool, string>(false, "");
            string clientsString = teamspeakService.GetOnlineTeamspeakClients();
            if (string.IsNullOrEmpty(clientsString)) return new Tuple<bool, string>(false, "");
            JObject clientsObject = JObject.Parse(clientsString);
            HashSet<TeamspeakClientSnapshot> onlineClients = JsonConvert.DeserializeObject<HashSet<TeamspeakClientSnapshot>>(clientsObject["clients"].ToString());
            foreach (TeamspeakClientSnapshot client in onlineClients.Where(x => x != null)) {
                if (account.teamspeakIdentities.Any(y => y == client.clientDbId)) {
                    return new Tuple<bool, string>(true, client.clientName);
                }
            }

            return new Tuple<bool, string>(false, "");
        }

        private static DateTime GetNextCandidateOp() => DateTime.Now;

        private double GetAverageProcessingTime() {
            List<Account> waitingApplications = accountService.Get(x => x.application != null && x.application.state != ApplicationState.WAITING).ToList();
            double days = waitingApplications.Sum(x => (x.application.dateAccepted - x.application.dateCreated).TotalDays);
            double time = Math.Round(days / waitingApplications.Count, 1);
            return time;
        }
    }
}
