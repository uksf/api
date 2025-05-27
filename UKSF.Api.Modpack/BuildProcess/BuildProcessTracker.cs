using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using UKSF.Api.Core;

namespace UKSF.Api.Modpack.BuildProcess;

public interface IBuildProcessTracker
{
    void RegisterProcess(int processId, string buildId, string description);
    void UnregisterProcess(int processId);
    IEnumerable<TrackedProcess> GetTrackedProcesses();
    IEnumerable<TrackedProcess> GetTrackedProcessesForBuild(string buildId);
    int KillTrackedProcesses(string buildId = null);
}

public record TrackedProcess(int ProcessId, string BuildId, string Description, DateTime StartTime);

public class BuildProcessTracker(IUksfLogger logger) : IBuildProcessTracker
{
    private readonly ConcurrentDictionary<int, TrackedProcess> _trackedProcesses = new();

    public void RegisterProcess(int processId, string buildId, string description)
    {
        var trackedProcess = new TrackedProcess(processId, buildId, description, DateTime.UtcNow);

        // Use indexer to allow updates (last registration wins)
        _trackedProcesses[processId] = trackedProcess;
    }

    public void UnregisterProcess(int processId)
    {
        _trackedProcesses.TryRemove(processId, out _);
    }

    public IEnumerable<TrackedProcess> GetTrackedProcesses()
    {
        // Clean up processes that no longer exist
        CleanupDeadProcesses();
        return _trackedProcesses.Values.ToList();
    }

    public IEnumerable<TrackedProcess> GetTrackedProcessesForBuild(string buildId)
    {
        CleanupDeadProcesses();
        return _trackedProcesses.Values.Where(p => p.BuildId == buildId).ToList();
    }

    public int KillTrackedProcesses(string buildId = null)
    {
        var processesToKill = buildId == null ? _trackedProcesses.Values.ToList() : _trackedProcesses.Values.Where(p => p.BuildId == buildId).ToList();

        var killedCount = 0;
        foreach (var trackedProcess in processesToKill)
        {
            try
            {
                var process = Process.GetProcessById(trackedProcess.ProcessId);
                if (!process.HasExited)
                {
                    process.Kill(true); // Kill entire process tree
                    killedCount++;
                }

                _trackedProcesses.TryRemove(trackedProcess.ProcessId, out _);
            }
            catch (ArgumentException)
            {
                // Process no longer exists, just remove it from tracking
                _trackedProcesses.TryRemove(trackedProcess.ProcessId, out _);
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to kill tracked process {trackedProcess.ProcessId}", ex);
            }
        }

        return killedCount;
    }

    private void CleanupDeadProcesses()
    {
        var deadProcesses = new List<int>();

        foreach (var (processId, _) in _trackedProcesses)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    deadProcesses.Add(processId);
                }
            }
            catch (ArgumentException)
            {
                // Process no longer exists
                deadProcesses.Add(processId);
            }
            catch (Win32Exception)
            {
                // Access denied - process exists but we can't access it
                // This can happen when trying to access system processes or processes owned by other users
                // We'll leave it in tracking since we can't determine if it's dead
            }
        }

        foreach (var processId in deadProcesses)
        {
            _trackedProcesses.TryRemove(processId, out _);
        }
    }
}
