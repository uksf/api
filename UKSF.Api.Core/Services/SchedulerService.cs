using System.Collections.Concurrent;
using System.Text.Json;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Services;

public interface ISchedulerService
{
    void Load();
    bool CheckJobScheduleChanged(string actionName, TimeSpan interval);
    Task CreateAndScheduleJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters);
    Task<ScheduledJob> CreateScheduledJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters);
    Task Cancel(Func<ScheduledJob, bool> predicate);
}

public class SchedulerService : ISchedulerService
{
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveTasks = new();
    private readonly IClock _clock;
    private readonly ISchedulerContext _context;
    private readonly IScheduledActionFactory _scheduledActionFactory;
    private readonly IUksfLogger _uksfLogger;

    public SchedulerService(ISchedulerContext context, IScheduledActionFactory scheduledActionFactory, IClock clock, IUksfLogger uksfLogger)
    {
        _context = context;
        _scheduledActionFactory = scheduledActionFactory;
        _clock = clock;
        _uksfLogger = uksfLogger;
    }

    public void Load()
    {
        _context.Get().ToList().ForEach(Schedule);
    }

    public bool CheckJobScheduleChanged(string actionName, TimeSpan interval)
    {
        var job = _context.GetSingle(x => x.Action == actionName);
        return job is null || interval != job.Interval;
    }

    public async Task CreateAndScheduleJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters)
    {
        var job = await CreateScheduledJob(next, interval, action, actionParameters);
        Schedule(job);
    }

    public async Task Cancel(Func<ScheduledJob, bool> predicate)
    {
        var job = _context.GetSingle(predicate);
        if (job == null)
        {
            return;
        }

        if (ActiveTasks.TryGetValue(job.Id, out var token))
        {
            token.Cancel();
            ActiveTasks.TryRemove(job.Id, out var _);
        }

        await _context.Delete(job);
    }

    public async Task<ScheduledJob> CreateScheduledJob(DateTime next, TimeSpan interval, string action, params object[] actionParameters)
    {
        ScheduledJob job = new() { Next = next, Action = action };
        if (actionParameters.Length > 0)
        {
            job.ActionParameters = JsonSerializer.Serialize(actionParameters, DefaultJsonSerializerOptions.Options);
        }

        if (interval != TimeSpan.Zero)
        {
            job.Interval = interval;
            job.Repeat = true;
        }

        await _context.Add(job);
        return job;
    }

    private void Schedule(ScheduledJob job)
    {
        CancellationTokenSource token = new();
        _ = Task.Run(
            async () =>
            {
                var now = _clock.UtcNow();
                if (now < job.Next)
                {
                    var delay = job.Next - now;
                    await Task.Delay(delay, token.Token);
                    if (IsCancelled(job, token))
                    {
                        return;
                    }
                }
                else
                {
                    if (job.Repeat)
                    {
                        var nowLessInterval = now - job.Interval;
                        while (job.Next < nowLessInterval)
                        {
                            job.Next += job.Interval;
                        }
                    }
                }

                try
                {
                    await ExecuteAction(job);
                }
                catch (Exception exception)
                {
                    _uksfLogger.LogError(exception);
                }

                if (job.Repeat)
                {
                    job.Next += job.Interval;
                    await SetNext(job);
                    Schedule(job);
                }
                else
                {
                    await _context.Delete(job);
                    ActiveTasks.TryRemove(job.Id, out _);
                }
            },
            token.Token
        );
        ActiveTasks[job.Id] = token;
    }

    private async Task SetNext(ScheduledJob job)
    {
        await _context.Update(job.Id, x => x.Next, job.Next);
    }

    private bool IsCancelled(MongoObject job, CancellationTokenSource token)
    {
        if (token.IsCancellationRequested)
        {
            return true;
        }

        return _context.GetSingle(job.Id) == null;
    }

    private async Task ExecuteAction(ScheduledJob job)
    {
        var action = _scheduledActionFactory.GetScheduledAction(job.Action);
        var parameters = job.ActionParameters == null ? null : JsonSerializer.Deserialize<object[]>(job.ActionParameters, DefaultJsonSerializerOptions.Options);
        await action.Run(parameters);
    }
}
