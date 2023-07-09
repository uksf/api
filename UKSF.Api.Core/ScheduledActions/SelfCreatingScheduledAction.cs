using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.ScheduledActions;

public class SelfCreatingScheduledAction : ISelfCreatingScheduledAction
{
    private readonly IHostEnvironment _currentEnvironment;
    private readonly ISchedulerContext _schedulerContext;
    private readonly ISchedulerService _schedulerService;

    protected SelfCreatingScheduledAction(ISchedulerService schedulerService, ISchedulerContext schedulerContext, IHostEnvironment currentEnvironment)
    {
        _schedulerService = schedulerService;
        _schedulerContext = schedulerContext;
        _currentEnvironment = currentEnvironment;
    }

    public virtual DateTime NextRun => throw new UksfException($"Undefined next run date for action {Name}", 500);
    public virtual TimeSpan RunInterval => throw new UksfException($"Undefined run interval for action {Name}", 500);
    public virtual string Name => "UNNAMED ACTION";

    public async Task CreateSelf()
    {
        if (_currentEnvironment.IsDevelopment())
        {
            return;
        }

        if (_schedulerContext.GetSingle(x => x.Action == Name) == null)
        {
            await _schedulerService.CreateScheduledJob(NextRun, RunInterval, Name);
        }
    }

    public virtual Task Run(params object[] parameters)
    {
        return Task.FromResult(Task.CompletedTask);
    }

    public Task Reset()
    {
        return Task.CompletedTask;
    }
}
