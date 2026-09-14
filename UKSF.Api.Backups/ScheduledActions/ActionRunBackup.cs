using UKSF.Api.Backups.Services;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Backups.ScheduledActions;

public interface IActionRunBackup : ISelfCreatingScheduledAction;

public class ActionRunBackup(ISchedulerService schedulerService, IHostEnvironment currentEnvironment, IClock clock, IBackupRunner backupRunner)
    : SelfCreatingScheduledAction(schedulerService, currentEnvironment), IActionRunBackup
{
    private const string ActionName = nameof(ActionRunBackup);
    private const int BackupUkHour = 4;

    public override DateTime NextRun => NextRunAfter(clock.UtcNow());
    public override TimeSpan RunInterval => TimeSpan.FromDays(1);
    public override string Name => ActionName;

    // UK wall clock, so the run stays at 04:00 across DST rather than drifting to 03:00 or 05:00.
    public override DateTime NextRunAfter(DateTime previous)
    {
        return clock.NextUkHourUtc(previous, BackupUkHour);
    }

    public override async Task Run(params object[] parameters)
    {
        await backupRunner.Run();
    }
}
