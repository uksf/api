using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public class GameDataExportService : IGameDataExportService
{
    private const int DefaultPollMs = 2000;
    private const int DefaultTimeoutSeconds = 600;
    private const int FileSanityFloorBytes = 1024;

    private readonly IGameDataExportProcessLauncher _launcher;
    private readonly IGameDataExportsContext _context;
    private readonly IProcessUtilities _processUtilities;
    private readonly IVariablesService _variablesService;
    private readonly IUksfLogger _logger;
    private readonly IArmaSyntheticLaunchGate _gate;
    private readonly int _pollMs;
    private readonly int _timeoutSeconds;

    private volatile CurrentRun _current;

    private record CurrentRun(string RunId, int ProcessId, GameDataExportLaunchResult Launch, DateTime StartedAt, GameDataExportStatus Status);

    public GameDataExportService(
        IGameDataExportProcessLauncher launcher,
        IGameDataExportsContext context,
        IProcessUtilities processUtilities,
        IVariablesService variablesService,
        IUksfLogger logger,
        IArmaSyntheticLaunchGate gate
    ) : this(launcher, context, processUtilities, variablesService, logger, gate, DefaultPollMs, DefaultTimeoutSeconds) { }

    internal GameDataExportService(
        IGameDataExportProcessLauncher launcher,
        IGameDataExportsContext context,
        IProcessUtilities processUtilities,
        IVariablesService variablesService,
        IUksfLogger logger,
        IArmaSyntheticLaunchGate gate,
        int pollMs,
        int timeoutSeconds
    )
    {
        _launcher = launcher;
        _context = context;
        _processUtilities = processUtilities;
        _variablesService = variablesService;
        _logger = logger;
        _gate = gate;
        _pollMs = pollMs;
        _timeoutSeconds = timeoutSeconds;
    }

    public TriggerResult Trigger(string modpackVersion)
    {
        var runId = Guid.NewGuid().ToString();
        if (!_gate.TryAcquire(runId))
        {
            return new TriggerResult(TriggerOutcome.AlreadyRunning, _gate.CurrentRunId ?? "");
        }

        try
        {
            var launch = _launcher.Launch(modpackVersion);
            _current = new CurrentRun(runId, launch.ProcessId, launch, DateTime.UtcNow, GameDataExportStatus.Running);
            _ = Task.Run(() => RunWatcherAsync(modpackVersion, launch));
            return new TriggerResult(TriggerOutcome.Started, runId);
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    public GameDataExportStatusResponse GetStatus()
    {
        var current = _current;
        return current is null
            ? new GameDataExportStatusResponse("", GameDataExportStatus.Pending, null)
            : new GameDataExportStatusResponse(current.RunId, current.Status, current.StartedAt);
    }

    private async Task RunWatcherAsync(string modpackVersion, GameDataExportLaunchResult launch)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_timeoutSeconds);

        try
        {
            // Process exit is the completion signal — the SQF calls "uksf" callExtension
            // ["fileExportFinish", []] when traversal finishes, and the Rust extension does
            // close + std::process::exit(0). Files are only considered final once arma exits.
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(_pollMs);

                if (_processUtilities.IsProcessAlive(launch.ProcessId)) continue;

                await FinishAsync(modpackVersion, launch, GameDataExportStatus.Success);
                return;
            }

            // Wall-clock timeout — kill the process and salvage whatever's on disk.
            KillProcess(launch.ProcessId);
            await FinishAsync(modpackVersion, launch, GameDataExportStatus.FailedTimeout, $"Export did not complete within {_timeoutSeconds} s.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            await FinishAsync(modpackVersion, launch, GameDataExportStatus.FailedLaunch, ex.Message);
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
        GameDataExportLaunchResult launch,
        GameDataExportStatus provisionalStatus,
        string failureDetail = null
    )
    {
        var configRoot = _variablesService.GetVariable("SERVER_PATH_CONFIG_EXPORT").AsString();
        var settingsRoot = _variablesService.GetVariable("SERVER_PATH_SETTINGS_EXPORT").AsString();

        var specs = new[]
        {
            new FileSpec(launch.ConfigGlob, configRoot, $"config_{modpackVersion}.cpp", FileSanityFloorBytes, ValidateJson: false),
            new FileSpec(launch.CbaSettingsGlob, settingsRoot, $"cba_settings_{modpackVersion}.sqf", MinBytes: 1, ValidateJson: false),
            new FileSpec(launch.CbaSettingsReferenceGlob, settingsRoot, $"cba_settings_reference_{modpackVersion}.json", MinBytes: 16, ValidateJson: true)
        };

        var results = specs.Select(s => CopyOne(launch.ExpectedOutputDirectory, s)).ToArray();
        var (configRes, settingsRes, refRes) = (results[0], results[1], results[2]);

        var truncatedSeen = results.Any(r => r.Truncated);
        var hasConfig = configRes.Copied;
        var hasSettings = settingsRes.Copied;
        var hasReference = refRes.Copied;
        var gameVersion = configRes.GameVersion ?? settingsRes.GameVersion ?? refRes.GameVersion ?? "unknown";

        var status = provisionalStatus switch
        {
            GameDataExportStatus.FailedLaunch                  => GameDataExportStatus.FailedLaunch,
            GameDataExportStatus.FailedTimeout                 => GameDataExportStatus.FailedTimeout,
            _ when truncatedSeen                               => GameDataExportStatus.FailedTruncated,
            _ when !hasConfig && !hasSettings && !hasReference => GameDataExportStatus.FailedNoOutput,
            _ when hasConfig && hasSettings && hasReference    => GameDataExportStatus.Success,
            _                                                  => GameDataExportStatus.PartialSuccess
        };

        var record = new DomainGameDataExport
        {
            ModpackVersion = modpackVersion,
            GameVersion = gameVersion,
            TriggeredAt = _current?.StartedAt ?? DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = status,
            HasConfig = hasConfig,
            HasCbaSettings = hasSettings,
            HasCbaSettingsReference = hasReference,
            FailureDetail = failureDetail
        };
        await _context.Add(record);

        if (_current is not null)
        {
            _current = _current with { Status = status };
        }

        _logger.LogInfo($"GameDataExport run {_current?.RunId}: {status}{(failureDetail is null ? "" : " — " + failureDetail)}");
    }

    private record FileSpec(string Glob, string DestRoot, string DestName, int MinBytes, bool ValidateJson);

    private record CopyResult(bool Copied, bool Truncated, string GameVersion);

    private static CopyResult CopyOne(string sourceDir, FileSpec spec)
    {
        if (!Directory.Exists(sourceDir)) return new CopyResult(false, false, null);

        var src = Directory.EnumerateFiles(sourceDir, spec.Glob).FirstOrDefault();
        if (src is null) return new CopyResult(false, false, null);

        var info = new FileInfo(src);
        if (info.Length < spec.MinBytes) return new CopyResult(false, true, null);

        Directory.CreateDirectory(spec.DestRoot);
        var dest = Path.Combine(spec.DestRoot, spec.DestName);
        File.Copy(src, dest, overwrite: true);

        if (spec.ValidateJson)
        {
            try
            {
                using var stream = File.OpenRead(dest);
                using var _ = System.Text.Json.JsonDocument.Parse(stream);
            }
            catch (System.Text.Json.JsonException)
            {
                File.Delete(dest);
                return new CopyResult(false, true, null);
            }
        }

        var name = Path.GetFileNameWithoutExtension(src);
        var parts = name.Split('_');
        var gameVersion = parts.Length >= 2 ? parts[1] : null;

        return new CopyResult(true, false, gameVersion);
    }
}
