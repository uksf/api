using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Newtonsoft.Json;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Data {
    public class SchedulerService : DataService<ScheduledJob>, ISchedulerService {
        private readonly IHostEnvironment currentEnvironment;
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> ACTIVE_TASKS = new ConcurrentDictionary<string, CancellationTokenSource>();

        public SchedulerService(IMongoDatabase database, IHostEnvironment currentEnvironment) : base(database, "scheduledJobs") => this.currentEnvironment = currentEnvironment;

        public async void Load(bool integration = false) {
            if (integration) {
                Get(x => x.type == ScheduledJobType.INTEGRATION).ForEach(Schedule);
            } else {
                if (!currentEnvironment.IsDevelopment()) await AddUnique();
                Get(x => x.type != ScheduledJobType.INTEGRATION).ForEach(Schedule);
            }
        }

        public async Task Create(DateTime next, TimeSpan interval, ScheduledJobType type, string action, params object[] actionParameters) {
            ScheduledJob job = new ScheduledJob {next = next, action = action, type = type};
            if (actionParameters.Length > 0) {
                job.actionParameters = JsonConvert.SerializeObject(actionParameters);
            }

            if (interval != TimeSpan.Zero) {
                job.interval = interval;
                job.repeat = true;
            }

            await Add(job);
            Schedule(job);
        }

        public async Task Cancel(Func<ScheduledJob, bool> predicate) {
            ScheduledJob job = GetSingle(predicate);
            if (job == null) return;
            if (ACTIVE_TASKS.TryGetValue(job.id, out CancellationTokenSource token)) {
                token.Cancel();
                ACTIVE_TASKS.TryRemove(job.id, out CancellationTokenSource _);
            }

            await Delete(job.id);
        }

        private void Schedule(ScheduledJob job) {
            CancellationTokenSource token = new CancellationTokenSource();
            Task unused = Task.Run(
                async () => {
                    DateTime now = DateTime.Now;
                    if (now < job.next) {
                        TimeSpan delay = job.next - now;
                        await Task.Delay(delay, token.Token);
                        if (IsCancelled(job, token)) return;
                    } else {
                        if (job.repeat) {
                            DateTime nowLessInterval = now - job.interval;
                            while (job.next < nowLessInterval) {
                                job.next += job.interval;
                            }
                        }
                    }

                    try {
                        ExecuteAction(job);
                    } catch (Exception exception) {
                        LogWrapper.Log(exception);
                    }

                    if (job.repeat) {
                        job.next += job.interval;
                        await SetNext(job);
                        Schedule(job);
                    } else {
                        await Delete(job.id);
                        ACTIVE_TASKS.TryRemove(job.id, out CancellationTokenSource _);
                    }
                },
                token.Token
            );
            ACTIVE_TASKS[job.id] = token;
        }

        // ReSharper disable once UnusedMember.Local
        private async Task AddUnique() {
            if (GetSingle(x => x.type == ScheduledJobType.LOG_PRUNE) == null) {
                await Create(DateTime.Today.AddDays(1), TimeSpan.FromDays(1), ScheduledJobType.LOG_PRUNE, nameof(SchedulerActionHelper.PruneLogs));
            }

            if (GetSingle(x => x.type == ScheduledJobType.TEAMSPEAK_SNAPSHOT) == null) {
                await Create(DateTime.Today.AddDays(1), TimeSpan.FromMinutes(5), ScheduledJobType.TEAMSPEAK_SNAPSHOT, nameof(SchedulerActionHelper.TeamspeakSnapshot));
            }

            if (GetSingle(x => x.type == ScheduledJobType.DISCORD_VOTE_ANNOUNCEMENT) == null) {
                await Create(DateTime.Today.AddHours(19), TimeSpan.FromDays(1), ScheduledJobType.DISCORD_VOTE_ANNOUNCEMENT, nameof(SchedulerActionHelper.DiscordVoteAnnouncement));
            }
        }

        private async Task SetNext(ScheduledJob job) {
            await Update(job.id, "next", job.next);
        }

        private bool IsCancelled(ScheduledJob job, CancellationTokenSource token) {
            if (token.IsCancellationRequested) return true;
            return GetSingle(job.id) == null;
        }

        private static void ExecuteAction(ScheduledJob job) {
            MethodInfo action = typeof(SchedulerActionHelper).GetMethod(job.action);
            if (action == null) {
                LogWrapper.Log($"Failed to find action '{job.action}' for scheduled job");
                return;
            }

            object[] parameters = job.actionParameters == null ? null : JsonConvert.DeserializeObject<object[]>(job.actionParameters);
            action.Invoke(null, parameters);
        }
    }
}
