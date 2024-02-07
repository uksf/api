using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.ScheduledActions;

public class SelfCreatingScheduledAction : ISelfCreatingScheduledAction
{
    private readonly IHostEnvironment _currentEnvironment;
    private readonly ISchedulerService _schedulerService;

    protected SelfCreatingScheduledAction(ISchedulerService schedulerService, IHostEnvironment currentEnvironment)
    {
        _schedulerService = schedulerService;
        _currentEnvironment = currentEnvironment;
    }

    public virtual DateTime NextRun => throw new UksfException($"Undefined next run date for action {Name}", 500);
    public virtual TimeSpan RunInterval => throw new UksfException($"Undefined run interval for action {Name}", 500);
    public virtual string Name => "UNNAMED ACTION";
    public virtual bool RunOnCreate => false;

    public async Task CreateSelf()
    {
        if (_currentEnvironment.IsDevelopment())
        {
            return;
        }

        if (_schedulerService.CheckJobScheduleChanged(Name, RunInterval))
        {
            await _schedulerService.CreateScheduledJob(NextRun, RunInterval, Name);
        }

        if (RunOnCreate)
        {
            _ = Task.Run(() => Run());
        }
    }

    public virtual Task Run(params object[] parameters)
    {
        return Task.FromResult(Task.CompletedTask);
    }

    public virtual Task Reset()
    {
        return Task.CompletedTask;
    }
}
