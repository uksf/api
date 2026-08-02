using Microsoft.Extensions.Hosting;
using UKSF.Api.Core;

namespace UKSF.Api.Backups.Services;

public class BackupStartupCheck(IBackupWatchdog backupWatchdog, IHostEnvironment currentEnvironment, IUksfLogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (currentEnvironment.IsDevelopment())
        {
            return;
        }

        try
        {
            await backupWatchdog.CheckOnStartup();
        }
        catch (Exception exception)
        {
            logger.LogError($"Backup startup check failed: {exception.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
