using System.Collections.Concurrent;
using System.Text;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public class DevRunService : IDevRunService
{
    private const int DefaultPollMs = 2000;
    private const int DefaultTimeoutSeconds = 300;
    private const int MaxTimeoutSeconds = 900;
    private const int InlineResultBytesMax = 1_048_576;
    private const int DiskResultBytesMax = 14 * 1_048_576;
    private const int LogLineMaxChars = 4096;
    private const int LogArrayCap = 10_000;

    private readonly IDevRunLauncher _launcher;
    private readonly IDevRunsContext _context;
    private readonly IProcessUtilities _processUtilities;
    private readonly IArmaSyntheticLaunchGate _gate;
    private readonly IVariablesService _variablesService;
    private readonly IUksfLogger _logger;
    private readonly int _pollMs;
    private readonly int _defaultTimeoutSeconds;
    private readonly ConcurrentDictionary<string, int> _activePids = new();

    // Production constructor — used by DI.
    public DevRunService(
        IDevRunLauncher launcher,
        IDevRunsContext context,
        IProcessUtilities processUtilities,
        IArmaSyntheticLaunchGate gate,
        IVariablesService variablesService,
        IUksfLogger logger
    ) : this(launcher, context, processUtilities, gate, variablesService, logger, DefaultPollMs, DefaultTimeoutSeconds) { }

    // Test constructor — allows short poll and timeout intervals.
    internal DevRunService(
        IDevRunLauncher launcher,
        IDevRunsContext context,
        IProcessUtilities processUtilities,
        IArmaSyntheticLaunchGate gate,
        IVariablesService variablesService,
        IUksfLogger logger,
        int pollMs,
        int defaultTimeoutSeconds
    )
    {
        _launcher = launcher;
        _context = context;
        _processUtilities = processUtilities;
        _gate = gate;
        _variablesService = variablesService;
        _logger = logger;
        _pollMs = pollMs;
        _defaultTimeoutSeconds = defaultTimeoutSeconds;
    }

    public DevRunTriggerResult Trigger(string sqf, IReadOnlyList<string> mods, int? timeoutSeconds, string worldName = null)
    {
        var runId = Guid.NewGuid().ToString();
        if (!_gate.TryAcquire(runId))
        {
            return new DevRunTriggerResult(DevRunTriggerOutcome.AlreadyRunning, _gate.CurrentRunId ?? "");
        }

        try
        {
            var effectiveTimeout = Math.Min(timeoutSeconds ?? _defaultTimeoutSeconds, MaxTimeoutSeconds);

            var record = new DomainDevRun
            {
                RunId = runId,
                Sqf = sqf,
                Mods = mods,
                StartedAt = DateTime.UtcNow,
                Status = DevRunStatus.Running
            };
            _context.Add(record).GetAwaiter().GetResult();

            var launch = _launcher.Launch(runId, sqf, mods, worldName);
            _activePids[runId] = launch.ProcessId;
            _ = Task.Run(() => RunWatcherAsync(launch.ProcessId, record, effectiveTimeout));

            return new DevRunTriggerResult(DevRunTriggerOutcome.Started, runId);
        }
        catch (InvalidModPathException ex)
        {
            _gate.Release();
            return new DevRunTriggerResult(DevRunTriggerOutcome.BadModPaths, runId, ex.MissingPaths);
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    public DevRunStatusResponse GetStatus(string runId)
    {
        var record = _context.GetSingle(x => x.RunId == runId);
        if (record is null)
        {
            return null;
        }

        var resultPreview = record.Result is null ? null : record.Result[..Math.Min(256, record.Result.Length)];
        return new DevRunStatusResponse(record.RunId, record.Status, record.StartedAt, record.CompletedAt, resultPreview, record.FailureDetail);
    }

    public async Task AppendLogAsync(string runId, string line)
    {
        var record = _context.GetSingle(x => x.RunId == runId);
        if (record is null)
        {
            return;
        }

        if (record.Logs.Count >= LogArrayCap)
        {
            if (!record.LogsTruncated)
            {
                await _context.Update(record.Id, x => x.LogsTruncated, true);
            }

            return;
        }

        var truncatedLine = line.Length > LogLineMaxChars ? line[..4090] + "[...]" : line;
        record.Logs.Add(new DevRunLogEntry(DateTime.UtcNow, truncatedLine));
        await _context.Update(record.Id, x => x.Logs, record.Logs);
    }

    public async Task AppendResultAsync(string runId, string payload)
    {
        var record = _context.GetSingle(x => x.RunId == runId);
        if (record is null)
        {
            return;
        }

        var byteLen = Encoding.UTF8.GetByteCount(payload);
        if (byteLen > DiskResultBytesMax)
        {
            await SetStatusAsync(record, DevRunStatus.FailedTooLarge, $"Result {byteLen} B exceeds {DiskResultBytesMax} B cap.");
        }
        else if (byteLen <= InlineResultBytesMax)
        {
            await _context.Update(record.Id, x => x.Result, payload);
        }
        else
        {
            var storageRoot = _variablesService.GetVariable("SERVER_PATH_DEV_RUN_RESULTS").AsString();
            Directory.CreateDirectory(storageRoot);
            var filePath = Path.Combine(storageRoot, $"{runId}.txt");
            await File.WriteAllTextAsync(filePath, payload, Encoding.UTF8);
            await _context.Update(record.Id, x => x.ResultFilePath, filePath);
        }
    }

    public async Task FinishAsync(string runId)
    {
        var record = _context.GetSingle(x => x.RunId == runId);
        if (record is null)
        {
            return;
        }

        if (record.Status == DevRunStatus.Running)
        {
            await _context.Update(record.Id, x => x.Status, DevRunStatus.Success);
            await _context.Update(record.Id, x => x.CompletedAt, (DateTime?)DateTime.UtcNow);
        }

        // Kill the spawned process so the watcher's IsProcessAlive check exits the
        // poll loop and releases the launch gate. Without this the gate is held
        // until wall-clock timeout even after a successful finish.
        if (_activePids.TryRemove(runId, out var pid))
        {
            KillProcess(pid);
        }
    }

    private async Task RunWatcherAsync(int pid, DomainDevRun record, int timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(_pollMs);

                if (_processUtilities.IsProcessAlive(pid))
                {
                    continue;
                }

                var current = _context.GetSingle(x => x.RunId == record.RunId);
                if (current?.Status == DevRunStatus.Running)
                {
                    await SetStatusAsync(current, DevRunStatus.FailedNoOutput, "Process exited without signalling completion.");
                }

                return;
            }

            KillProcess(pid);

            var afterKill = _context.GetSingle(x => x.RunId == record.RunId);
            if (afterKill?.Status == DevRunStatus.Running)
            {
                await SetStatusAsync(afterKill, DevRunStatus.FailedTimeout, $"Dev run did not complete within {timeoutSeconds} s.");
            }
        }
        finally
        {
            _activePids.TryRemove(record.RunId, out _);
            _gate.Release();
        }
    }

    private async Task SetStatusAsync(DomainDevRun record, DevRunStatus status, string failureDetail = null)
    {
        await _context.Update(record.Id, x => x.Status, status);
        await _context.Update(record.Id, x => x.CompletedAt, (DateTime?)DateTime.UtcNow);
        if (failureDetail is not null)
        {
            await _context.Update(record.Id, x => x.FailureDetail, failureDetail);
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
}
