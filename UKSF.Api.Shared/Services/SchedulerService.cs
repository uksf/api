using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Models;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Services {
    public interface ISchedulerService : IDataBackedService<ISchedulerDataService> {
        void Load();
        Task CreateAndScheduleJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters);
        Task<ScheduledJob> CreateScheduledJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters);
        Task Cancel(Func<ScheduledJob, bool> predicate);
    }

    public class SchedulerService : DataBackedService<ISchedulerDataService>, ISchedulerService {
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> ACTIVE_TASKS = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ILogger _logger;
        private readonly IScheduledActionFactory _scheduledActionFactory;

        public SchedulerService(ISchedulerDataService data, IScheduledActionFactory scheduledActionFactory, ILogger logger) : base(data) {
            _scheduledActionFactory = scheduledActionFactory;
            _logger = logger;
        }

        public void Load() {
            Data.Get().ToList().ForEach(Schedule);
        }

        public async Task CreateAndScheduleJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters) {
            ScheduledJob job = await CreateScheduledJob(next, interval, action, actionParameters);
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

        public async Task<ScheduledJob> CreateScheduledJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters) {
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
                        _logger.LogError(exception);
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

        private async Task SetNext(ScheduledJob job) {
            await Data.Update(job.id, "next", job.next);
        }

        private bool IsCancelled(DatabaseObject job, CancellationTokenSource token) {
            if (token.IsCancellationRequested) return true;
            return Data.GetSingle(job.id) == null;
        }

        private void ExecuteAction(ScheduledJob job) {
            IScheduledAction action = _scheduledActionFactory.GetScheduledAction(job.action);
            object[] parameters = job.actionParameters == null ? null : JsonConvert.DeserializeObject<object[]>(job.actionParameters);
            action.Run(parameters);
        }
    }
}
