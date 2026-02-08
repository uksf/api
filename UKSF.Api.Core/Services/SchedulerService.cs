using System.Collections.Concurrent;
using System.Text.Json;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface ISchedulerService
{
    void Load();
    Task CreateAndScheduleJob(string actionName, DateTime next, TimeSpan interval, params object[] actionParameters);
    Task<DomainScheduledJob> CreateScheduledJob(string actionName, DateTime next, TimeSpan interval, params object[] actionParameters);
    Task Cancel(Func<DomainScheduledJob, bool> predicate);
}

public class SchedulerService(ISchedulerContext context, IScheduledActionFactory scheduledActionFactory, IClock clock, IUksfLogger uksfLogger)
    : ISchedulerService
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeTasks = new();

    public void Load()
    {
        context.Get().ToList().ForEach(Schedule);
    }

    public async Task CreateAndScheduleJob(string actionName, DateTime next, TimeSpan interval, params object[] actionParameters)
    {
        var job = await CreateScheduledJob(actionName, next, interval, actionParameters);
        Schedule(job);
    }

    public async Task Cancel(Func<DomainScheduledJob, bool> predicate)
    {
        var job = context.GetSingle(predicate);
        if (job == null)
        {
            return;
        }

        CancelAndDisposeToken(job.Id);
        await context.Delete(job);
    }

    public async Task<DomainScheduledJob> CreateScheduledJob(string actionName, DateTime next, TimeSpan interval, params object[] actionParameters)
    {
        var job = context.GetSingle(x => x.Action == actionName);
        if (job is not null)
        {
            if (job.Interval != interval)
            {
                await context.Update(job.Id, x => x.Interval, interval);
            }

            return job;
        }

        job = new DomainScheduledJob { Next = next, Action = actionName };
        if (actionParameters.Length > 0)
        {
            job.ActionParameters = JsonSerializer.Serialize(actionParameters, DefaultJsonSerializerOptions.Options);
        }

        if (interval != TimeSpan.Zero)
        {
            job.Interval = interval;
            job.Repeat = true;
        }

        await context.Add(job);
        return job;
    }

    private void Schedule(DomainScheduledJob job)
    {
        CancellationTokenSource token = new();
        CancelAndDisposeToken(job.Id);
        _activeTasks[job.Id] = token;

        _ = Task.Run(
            async () =>
            {
                var now = clock.UtcNow();
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
                    uksfLogger.LogError(exception);
                }

                if (job.Repeat)
                {
                    job.Next += job.Interval;
                    await SetNext(job);
                    Schedule(job);
                }
                else
                {
                    await context.Delete(job);
                    CancelAndDisposeToken(job.Id);
                }
            },
            token.Token
        );
    }

    private void CancelAndDisposeToken(string jobId)
    {
        if (_activeTasks.TryRemove(jobId, out var existingToken))
        {
            existingToken.Cancel();
            existingToken.Dispose();
        }
    }

    private async Task SetNext(DomainScheduledJob job)
    {
        await context.Update(job.Id, x => x.Next, job.Next);
    }

    private bool IsCancelled(MongoObject job, CancellationTokenSource token)
    {
        if (token.IsCancellationRequested)
        {
            return true;
        }

        return context.GetSingle(job.Id) == null;
    }

    private async Task ExecuteAction(DomainScheduledJob job)
    {
        var action = scheduledActionFactory.GetScheduledAction(job.Action);
        var parameters = job.ActionParameters == null ? null : JsonSerializer.Deserialize<object[]>(job.ActionParameters, DefaultJsonSerializerOptions.Options);
        await action.Run(parameters);
    }
}
