using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Utility.Models;
using UKSF.Api.Utility.ScheduledActions;
using UKSF.Api.Utility.Services.Data;

namespace UKSF.Api.Utility.Services {
    public interface ISchedulerService : IDataBackedService<ISchedulerDataService> {
        void Load();
        Task CreateAndSchedule(DateTime next, TimeSpan interval, string action, params object[] actionParameters);
        Task Cancel(Func<ScheduledJob, bool> predicate);
    }

    public class SchedulerService : DataBackedService<ISchedulerDataService>, ISchedulerService {
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> ACTIVE_TASKS = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly IHostEnvironment currentEnvironment;
        private readonly ILogger logger;
        private readonly IScheduledActionService scheduledActionService;

        public SchedulerService(ISchedulerDataService data, IScheduledActionService scheduledActionService, IHostEnvironment currentEnvironment, ILogger logger) : base(data) {
            this.scheduledActionService = scheduledActionService;
            this.currentEnvironment = currentEnvironment;
            this.logger = logger;
        }

        public async void Load() {
            await AddUnique();
            Data.Get().ToList().ForEach(Schedule);
        }

        public async Task CreateAndSchedule(DateTime next, TimeSpan interval, string action, params object[] actionParameters) {
            ScheduledJob job = await Create(next, interval, action, actionParameters);
            Schedule(job);
        }

        public async Task Cancel(Func<ScheduledJob, bool> predicate) {
            ScheduledJob job = Data.GetSingle(predicate);
            if (job == null) return;
            if (ACTIVE_TASKS.TryGetValue(job.id, out CancellationTokenSource token)) {
                token.Cancel();
                ACTIVE_TASKS.TryRemove(job.id, out CancellationTokenSource _);
            }

            await Data.Delete(job);
        }

        private async Task<ScheduledJob> Create(DateTime next, TimeSpan interval, string action, params object[] actionParameters) {
            ScheduledJob job = new ScheduledJob { next = next, action = action };
            if (actionParameters.Length > 0) {
                job.actionParameters = JsonConvert.SerializeObject(actionParameters);
            }

            if (interval != TimeSpan.Zero) {
                job.interval = interval;
                job.repeat = true;
            }

            await Data.Add(job);
            return job;
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
                        logger.LogError(exception);
                    }

                    if (job.repeat) {
                        job.next += job.interval;
                        await SetNext(job);
                        Schedule(job);
                    } else {
                        await Data.Delete(job);
                        ACTIVE_TASKS.TryRemove(job.id, out CancellationTokenSource _);
                    }
                },
                token.Token
            );
            ACTIVE_TASKS[job.id] = token;
        }

        // TODO: Move out of this bit
        private async Task AddUnique() {
            if (Data.GetSingle(x => x.action == InstagramImagesAction.ACTION_NAME) == null) {
                await Create(DateTime.Today, TimeSpan.FromMinutes(15), InstagramImagesAction.ACTION_NAME);
            }

            scheduledActionService.GetScheduledAction(InstagramImagesAction.ACTION_NAME).Run();

            if (!currentEnvironment.IsDevelopment()) {
                if (Data.GetSingle(x => x.action == InstagramTokenAction.ACTION_NAME) == null) {
                    await Create(DateTime.Today.AddDays(45), TimeSpan.FromDays(45), InstagramTokenAction.ACTION_NAME);
                }

                if (Data.GetSingle(x => x.action == PruneDataAction.ACTION_NAME) == null) {
                    await Create(DateTime.Today.AddDays(1), TimeSpan.FromDays(1), PruneDataAction.ACTION_NAME);
                }

                if (Data.GetSingle(x => x.action == TeamspeakSnapshotAction.ACTION_NAME) == null) {
                    await Create(DateTime.Today.AddMinutes(5), TimeSpan.FromMinutes(5), TeamspeakSnapshotAction.ACTION_NAME);
                }
            }
        }

        private async Task SetNext(ScheduledJob job) {
            await Data.Update(job.id, "next", job.next);
        }

        private bool IsCancelled(DatabaseObject job, CancellationTokenSource token) {
            if (token.IsCancellationRequested) return true;
            return Data.GetSingle(job.id) == null;
        }

        private void ExecuteAction(ScheduledJob job) {
            IScheduledAction action = scheduledActionService.GetScheduledAction(job.action);
            object[] parameters = job.actionParameters == null ? null : JsonConvert.DeserializeObject<object[]>(job.actionParameters);
            action.Run(parameters);
        }
    }
}