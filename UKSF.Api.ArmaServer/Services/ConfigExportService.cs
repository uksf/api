using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public class ConfigExportService : IConfigExportService
{
    private const int DefaultPollMs = 2000;
    private const int DefaultTimeoutSeconds = 600;
    private const int FileSanityFloorBytes = 1024;

    private readonly IConfigExportProcessLauncher _launcher;
    private readonly IGameConfigExportsContext _context;
    private readonly IProcessUtilities _processUtilities;
    private readonly IVariablesService _variablesService;
    private readonly IUksfLogger _logger;
    private readonly int _pollMs;
    private readonly int _timeoutSeconds;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile CurrentRun _current;

    private record CurrentRun(string RunId, int ProcessId, string ExpectedDir, string FilenameGlob, DateTime StartedAt, ConfigExportStatus Status);

    // Production constructor — used by DI.
    public ConfigExportService(
        IConfigExportProcessLauncher launcher,
        IGameConfigExportsContext context,
        IProcessUtilities processUtilities,
        IVariablesService variablesService,
        IUksfLogger logger
    ) : this(launcher, context, processUtilities, variablesService, logger, DefaultPollMs, DefaultTimeoutSeconds) { }

    // Test constructor — allows short poll and timeout intervals.
    internal ConfigExportService(
        IConfigExportProcessLauncher launcher,
        IGameConfigExportsContext context,
        IProcessUtilities processUtilities,
        IVariablesService variablesService,
        IUksfLogger logger,
        int pollMs,
        int timeoutSeconds
    )
    {
        _launcher = launcher;
        _context = context;
        _processUtilities = processUtilities;
        _variablesService = variablesService;
        _logger = logger;
        _pollMs = pollMs;
        _timeoutSeconds = timeoutSeconds;
    }

    public TriggerResult Trigger(string modpackVersion)
    {
        if (!_gate.Wait(0))
        {
            return new TriggerResult(TriggerOutcome.AlreadyRunning, _current?.RunId ?? "");
        }

        try
        {
            var runId = Guid.NewGuid().ToString();
            var launch = _launcher.Launch(modpackVersion);
            _current = new CurrentRun(
                runId,
                launch.ProcessId,
                launch.ExpectedOutputDirectory,
                launch.ExpectedFilenameGlob,
                DateTime.UtcNow,
                ConfigExportStatus.Running
            );
            // Detached background work — Task.Run; no await on the public path.
            _ = Task.Run(() => RunWatcherAsync(modpackVersion, launch));
            return new TriggerResult(TriggerOutcome.Started, runId);
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    public ConfigExportStatusResponse GetStatus()
    {
        var current = _current;
        return current is null
            ? new ConfigExportStatusResponse("", ConfigExportStatus.Pending, null)
            : new ConfigExportStatusResponse(current.RunId, current.Status, current.StartedAt);
    }

    private async Task RunWatcherAsync(string modpackVersion, ConfigExportLaunchResult launch)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_timeoutSeconds);

        string FindOutput()
        {
            if (!Directory.Exists(launch.ExpectedOutputDirectory)) return null;
            return Directory.EnumerateFiles(launch.ExpectedOutputDirectory, launch.ExpectedFilenameGlob).FirstOrDefault();
        }

        try
        {
            // Process exit is the completion signal — the SQF calls "uksf" callExtension
            // ["configExportFinish", []] when traversal finishes, and the Rust extension does
            // close + std::process::exit(0). The file is only considered final once arma exits.
            // Killing earlier truncates the export mid-flush.
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(_pollMs);

                var processAlive = _processUtilities.IsProcessAlive(launch.ProcessId);
                if (processAlive) continue;

                var foundFile = FindOutput();
                if (foundFile is not null)
                {
                    await FinishAsync(modpackVersion, launch, foundFile, ConfigExportStatus.Success);
                    return;
                }

                await FinishAsync(modpackVersion, launch, null, ConfigExportStatus.FailedNoOutput, "Process exited before output file appeared.");
                return;
            }

            // Wall-clock timeout reached — process never exited on its own.
            // Salvage: if the file is on disk it's worth keeping even if the process never
            // signalled completion. Status stays FailedTimeout (caller knows it's not a clean
            // run) but the artifact isn't lost.
            KillProcess(launch.ProcessId);
            var salvaged = FindOutput();
            await FinishAsync(modpackVersion, launch, salvaged, ConfigExportStatus.FailedTimeout, $"Export did not complete within {_timeoutSeconds} s.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            await FinishAsync(modpackVersion, launch, null, ConfigExportStatus.FailedLaunch, ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void KillProcess(int pid)
    {
        try
        {
            var process = _processUtilities.FindProcessById(pid);
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }
    }

    private async Task FinishAsync(
        string modpackVersion,
        ConfigExportLaunchResult launch,
        string sourceFile,
        ConfigExportStatus status,
        string failureDetail = null
    )
    {
        string storedPath = null;
        string gameVersion = null;

        // Copy file on Success or on FailedTimeout salvage — losing the artifact on a
        // timeout is worse than keeping a "process never signalled exit" record. Truncation
        // sanity check still applies to Success only; timeout salvage trusts whatever's on disk.
        var shouldStore = sourceFile is not null && (status == ConfigExportStatus.Success || status == ConfigExportStatus.FailedTimeout);
        if (shouldStore)
        {
            var fileInfo = new FileInfo(sourceFile);
            if (status == ConfigExportStatus.Success && (!fileInfo.Exists || fileInfo.Length < FileSanityFloorBytes))
            {
                status = ConfigExportStatus.FailedTruncated;
                failureDetail = $"Output file missing or below sanity floor ({fileInfo.Length} B).";
            }
            else if (fileInfo.Exists)
            {
                // Extract game version from "config_<gameVer>_uksf-<modpackVer>.cpp"
                var name = Path.GetFileNameWithoutExtension(sourceFile);
                var parts = name.Split('_');
                if (parts.Length >= 2)
                {
                    gameVersion = parts[1];
                }

                var storageRoot = _variablesService.GetVariable("SERVER_PATH_CONFIG_EXPORT").AsString();
                Directory.CreateDirectory(storageRoot);
                storedPath = Path.Combine(storageRoot, $"config_{modpackVersion}.cpp");
                File.Copy(sourceFile, storedPath, overwrite: true);
            }
        }

        var record = new DomainGameConfigExport
        {
            ModpackVersion = modpackVersion,
            GameVersion = gameVersion ?? "unknown",
            TriggeredAt = _current?.StartedAt ?? DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = status,
            FilePath = storedPath,
            FailureDetail = failureDetail
        };
        await _context.Add(record);

        if (_current is not null)
        {
            _current = _current with { Status = status };
        }

        _logger.LogInfo($"ConfigExport run {_current?.RunId}: {status}{(failureDetail is null ? "" : " — " + failureDetail)}");
    }
}
