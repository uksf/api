using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UKSF.Api.Base.Models;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Services {
    public interface ISchedulerService {
        void Load();
        Task CreateAndScheduleJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters);
        Task<ScheduledJob> CreateScheduledJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters);
        Task Cancel(Func<ScheduledJob, bool> predicate);
    }

    public class SchedulerService : ISchedulerService {
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> ACTIVE_TASKS = new();
        private readonly ISchedulerContext _context;
        private readonly ILogger _logger;
        private readonly IScheduledActionFactory _scheduledActionFactory;

        public SchedulerService(ISchedulerContext context, IScheduledActionFactory scheduledActionFactory, ILogger logger) {
            _context = context;
            _scheduledActionFactory = scheduledActionFactory;
            _logger = logger;
        }

        public void Load() {
            _context.Get().ToList().ForEach(Schedule);
        }

        public async Task CreateAndScheduleJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters) {
            ScheduledJob job = await CreateScheduledJob(next, interval, action, actionParameters);
            Schedule(job);
        }

        public async Task Cancel(Func<ScheduledJob, bool> predicate) {
            ScheduledJob job = _context.GetSingle(predicate);
            if (job == null) return;
            if (ACTIVE_TASKS.TryGetValue(job.Id, out CancellationTokenSource token)) {
                token.Cancel();
                ACTIVE_TASKS.TryRemove(job.Id, out CancellationTokenSource _);
            }

            await _context.Delete(job);
        }

        public async Task<ScheduledJob> CreateScheduledJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters) {
            ScheduledJob job = new() { Next = next, Action = action };
            if (actionParameters.Length > 0) {
                job.ActionParameters = JsonConvert.SerializeObject(actionParameters);
            }

            if (interval != TimeSpan.Zero) {
                job.Interval = interval;
                job.Repeat = true;
            }

            await _context.Add(job);
            return job;
        }

        private void Schedule(ScheduledJob job) {
            CancellationTokenSource token = new();
            Task unused = Task.Run(
                async () => {
                    DateTime now = DateTime.Now;
                    if (now < job.Next) {
                        TimeSpan delay = job.Next - now;
                        await Task.Delay(delay, token.Token);
                        if (IsCancelled(job, token)) return;
                    } else {
                        if (job.Repeat) {
                            DateTime nowLessInterval = now - job.Interval;
                            while (job.Next < nowLessInterval) {
                                job.Next += job.Interval;
                            }
                        }
                    }

                    try {
                        ExecuteAction(job);
                    } catch (Exception exception) {
                        _logger.LogError(exception);
                    }

                    if (job.Repeat) {
                        job.Next += job.Interval;
                        await SetNext(job);
                        Schedule(job);
                    } else {
                        await _context.Delete(job);
                        ACTIVE_TASKS.TryRemove(job.Id, out CancellationTokenSource _);
                    }
                },
                token.Token
            );
            ACTIVE_TASKS[job.Id] = token;
        }

        private async Task SetNext(ScheduledJob job) {
            await _context.Update(job.Id, x => x.Next, job.Next);
        }

        private bool IsCancelled(MongoObject job, CancellationTokenSource token) {
            if (token.IsCancellationRequested) return true;
            return _context.GetSingle(job.Id) == null;
        }

        private void ExecuteAction(ScheduledJob job) {
            IScheduledAction action = _scheduledActionFactory.GetScheduledAction(job.Action);
            object[] parameters = job.ActionParameters == null ? null : JsonConvert.DeserializeObject<object[]>(job.ActionParameters);
            Task unused = action.Run(parameters);
        }
    }
}
