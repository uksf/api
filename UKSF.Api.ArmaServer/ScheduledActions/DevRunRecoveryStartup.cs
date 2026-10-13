using Microsoft.Extensions.Hosting;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.ArmaServer.ScheduledActions;

public class DevRunRecoveryStartup(IGameServerHelpers helpers, IProcessUtilities processUtilities, IDevRunsContext context, IUksfLogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var devRunProcesses = processUtilities.GetProcessesWithCommandLine("arma3server").Where(x => IsDevRunProfile(x.CommandLine)).ToList();
            KillOrphanProcesses(devRunProcesses.Where(x => helpers.IsOwnedProcess(x.CommandLine)));
            var liveForeignProfileIds = devRunProcesses.Where(x => !helpers.IsOwnedProcess(x.CommandLine)).Select(x => ProfileId(x.CommandLine)).ToList();
            await MarkStuckRecordsFailed(liveForeignProfileIds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void KillOrphanProcesses(IEnumerable<ProcessCommandLineInfo> ownedDevRunProcesses)
    {
        foreach (var process in ownedDevRunProcesses)
        {
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

    private static readonly System.Text.RegularExpressions.Regex DevRunProfile = new(
        @"-profiles=""?[^\s""]*[/\\]DevRun_(\w+)",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
    );

    private static bool IsDevRunProfile(string commandLine) => !string.IsNullOrEmpty(commandLine) && DevRunProfile.IsMatch(commandLine);

    private static string ProfileId(string commandLine) => DevRunProfile.Match(commandLine).Groups[1].Value;

    private async Task MarkStuckRecordsFailed(List<string> liveForeignProfileIds)
    {
        var stuck = context.Get(x => x.Status == DevRunStatus.Running)
                           .Where(x => !liveForeignProfileIds.Any(id => x.RunId?.StartsWith(id, StringComparison.OrdinalIgnoreCase) == true))
                           .ToList();
        foreach (var record in stuck)
        {
            await context.Update(record.Id, x => x.Status, DevRunStatus.FailedLaunch);
            await context.Update(record.Id, x => x.CompletedAt, DateTime.UtcNow);
            await context.Update(record.Id, x => x.FailureDetail, "API restart while running.");
        }
    }
}
