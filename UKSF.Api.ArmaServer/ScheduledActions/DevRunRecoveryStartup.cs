using Microsoft.Extensions.Hosting;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.ArmaServer.ScheduledActions;

public class DevRunRecoveryStartup(IProcessUtilities processUtilities, IDevRunsContext context, IUksfLogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            KillOrphanProcesses();
            await MarkStuckRecordsFailed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void KillOrphanProcesses()
    {
        foreach (var process in processUtilities.GetProcessesWithCommandLine("arma3server"))
        {
            if (!IsDevRunProfile(process.CommandLine)) continue;
            try
            {
                var running = processUtilities.FindProcessById(process.ProcessId);
                if (running is { HasExited: false })
                {
                    running.Kill(true);
                    logger.LogInfo($"DevRunRecovery: killed orphan PID {process.ProcessId}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
        }
    }

    private static bool IsDevRunProfile(string commandLine) =>
        !string.IsNullOrEmpty(commandLine) &&
        System.Text.RegularExpressions.Regex.IsMatch(commandLine, @"-profiles=""?[^\s""]*[/\\]DevRun_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private async Task MarkStuckRecordsFailed()
    {
        var stuck = context.Get(x => x.Status == DevRunStatus.Running).ToList();
        foreach (var record in stuck)
        {
            await context.Update(record.Id, x => x.Status, DevRunStatus.FailedLaunch);
            await context.Update(record.Id, x => x.CompletedAt, DateTime.UtcNow);
            await context.Update(record.Id, x => x.FailureDetail, "API restart while running.");
        }
    }
}
