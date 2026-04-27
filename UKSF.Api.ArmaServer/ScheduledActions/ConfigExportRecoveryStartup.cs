using Microsoft.Extensions.Hosting;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.ScheduledActions;

public class ConfigExportRecoveryStartup(
    IGameServerHelpers helpers,
    IProcessUtilities processUtilities,
    IGameConfigExportsContext context,
    IVariablesService variablesService,
    IUksfLogger logger
) : IHostedService
{
    private const int SalvageWindowHours = 24;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SalvageRecentExportsAsync();
            KillOrphanProcesses();
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
            if (!helpers.IsConfigExportProcess(process.CommandLine)) continue;
            try
            {
                var runningProcess = processUtilities.FindProcessById(process.ProcessId);
                if (runningProcess is { HasExited: false })
                {
                    runningProcess.Kill(true);
                    logger.LogInfo($"ConfigExportRecovery: killed orphan PID {process.ProcessId}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
        }
    }

    private async Task SalvageRecentExportsAsync()
    {
        var storageRoot = variablesService.GetVariable("SERVER_PATH_CONFIG_EXPORT").AsString();
        if (!Directory.Exists(storageRoot)) return;

        var cutoff = DateTime.UtcNow.AddHours(-SalvageWindowHours);
        foreach (var file in Directory.EnumerateFiles(storageRoot, "config_*.cpp"))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc < cutoff) continue;

            var version = Path.GetFileNameWithoutExtension(file).Replace("config_", "");
            var existing = context.Get(x => x.ModpackVersion == version && x.Status == ConfigExportStatus.Success);
            if (existing.Any()) continue;

            await context.Add(
                new DomainGameConfigExport
                {
                    ModpackVersion = version,
                    GameVersion = "unknown",
                    TriggeredAt = info.CreationTimeUtc,
                    CompletedAt = info.LastWriteTimeUtc,
                    Status = ConfigExportStatus.Success,
                    FilePath = file,
                    FailureDetail = "Salvaged on API restart."
                }
            );
            logger.LogInfo($"ConfigExportRecovery: salvaged {file}");
        }
    }
}
