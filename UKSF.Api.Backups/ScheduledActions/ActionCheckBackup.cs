using UKSF.Api.Backups.Services;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Backups.ScheduledActions;

public interface IActionCheckBackup : ISelfCreatingScheduledAction;

public class ActionCheckBackup(ISchedulerService schedulerService, IHostEnvironment currentEnvironment, IClock clock, IBackupWatchdog backupWatchdog)
    : SelfCreatingScheduledAction(schedulerService, currentEnvironment), IActionCheckBackup
{
    private const string ActionName = nameof(ActionCheckBackup);
    private const int CheckUkHour = 6;

    public override DateTime NextRun => NextRunAfter(clock.UtcNow());
    public override TimeSpan RunInterval => TimeSpan.FromDays(1);
    public override string Name => ActionName;

    public override DateTime NextRunAfter(DateTime previous)
    {
        return clock.NextUkHourUtc(previous, CheckUkHour);
    }

    public override async Task Run(params object[] parameters)
    {
        await backupWatchdog.CheckToday();
    }
}
