using Microsoft.Extensions.Hosting;
using UKSF.Api.Core;

namespace UKSF.Api.Backups.Services;

public class BackupStartupCheck(IBackupWatchdog backupWatchdog, IHostEnvironment currentEnvironment, IUksfLogger logger) : IHostedService
{
    /// <summary>
    ///     Hosted services start before the Discord bot is activated, and alerting through a client that is still
    ///     connecting fails with "Cannot start an already running client". The overdue check waits for it.
    /// </summary>
    private static readonly TimeSpan AlertDelay = TimeSpan.FromMinutes(2);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (currentEnvironment.IsDevelopment())
        {
            return;
        }

        try
        {
            var interrupted = await backupWatchdog.ResolveInterrupted();
            if (interrupted > 0)
            {
                logger.LogWarning($"Marked {interrupted} interrupted backup run(s) as failed");
            }
        }
        catch (Exception exception)
        {
            logger.LogError($"Backup startup check failed: {exception.Message}");
        }

        _ = Task.Run(
            async () =>
            {
                await Task.Delay(AlertDelay, CancellationToken.None);

                try
                {
                    await backupWatchdog.CheckOnStartup();
                }
                catch (Exception exception)
                {
                    logger.LogError($"Backup startup alert failed: {exception.Message}");
                }
            },
            CancellationToken.None
        );
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
