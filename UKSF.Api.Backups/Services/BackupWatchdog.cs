using UKSF.Api.Backups.DataContext;
using UKSF.Api.Backups.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Backups.Services;

public interface IBackupWatchdog
{
    Task<DomainBackupRun> LastSuccess();
    Task CheckToday();
    Task CheckOnStartup();
}

/// <summary>
///     Catches the failure mode the old setup had no answer for: nothing ran at all, and nobody found out.
/// </summary>
public class BackupWatchdog(IBackupRunsContext backupRunsContext, IBackupAlertService backupAlertService, IClock clock) : IBackupWatchdog
{
    private static readonly TimeSpan StartupTolerance = TimeSpan.FromHours(26);

    public Task<DomainBackupRun> LastSuccess()
    {
        var last = backupRunsContext.Get(x => x.State == BackupRunState.Success).MaxBy(x => x.Started);
        return Task.FromResult(last);
    }

    /// <summary>Runs after the scheduled backup hour: no success today means the run failed, was skipped, or never fired.</summary>
    public async Task CheckToday()
    {
        var last = await LastSuccess();
        if (last is not null && last.Started >= clock.UtcNow().Date)
        {
            return;
        }

        var age = last is null ? "ever" : $"since {last.Started:yyyy-MM-dd HH:mm} UTC";
        await backupAlertService.Alert($"no successful backup today - last success {age}");
    }

    /// <summary>The API being down at 04:00 is the quiet way to miss a night, so a late start reports it.</summary>
    public async Task CheckOnStartup()
    {
        var last = await LastSuccess();
        if (last is not null && clock.UtcNow() - last.Started < StartupTolerance)
        {
            return;
        }

        var age = last is null ? "no backup has ever succeeded" : $"last success was {last.Started:yyyy-MM-dd HH:mm} UTC";
        await backupAlertService.Alert($"backups are overdue on startup - {age}");
    }
}
