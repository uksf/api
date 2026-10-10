using System.ComponentModel;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    private readonly Dictionary<(int ProcessId, DateTime? StartedAt), DateTime> _orphanFirstSeen = new();

    internal async Task<int> ReconcileOrphansAsync(DateTime now)
    {
        var owned = gameServerHelpers.GetGameServerArmaProcesses();
        if (owned.Count == 0)
        {
            _orphanFirstSeen.Clear();
            return 0;
        }

        var serverLocks = gameServersContext.Get().Select(server => server.Id).Distinct().Order().Select(GetServerLock).ToList();
        foreach (var serverLock in serverLocks)
        {
            await serverLock.WaitAsync();
        }

        try
        {
            var tracked = gameServersContext.Get().SelectMany(server => server.HeadlessClientProcessIds.Append(server.ProcessId ?? 0)).ToHashSet();
            var orphans = owned.Where(process => !tracked.Contains(process.ProcessId))
                               .Select(process => (process.ProcessId, StartedAt: StartTimeOfProcessId(process.ProcessId)))
                               .ToHashSet();

            foreach (var gone in _orphanFirstSeen.Keys.Where(key => !orphans.Contains(key)).ToList())
            {
                _orphanFirstSeen.Remove(gone);
            }

            foreach (var orphan in orphans)
            {
                if (!_orphanFirstSeen.TryAdd(orphan, now))
                {
                    if (now - _orphanFirstSeen[orphan] >= OrphanKillCeiling)
                    {
                        KillOrphan(orphan);
                        _orphanFirstSeen.Remove(orphan);
                    }
                }
                else
                {
                    logger.LogInfo(
                        $"Process monitor: untracked arma process {orphan.ProcessId} found, killing it if still untracked after {OrphanKillCeiling.TotalMinutes:F0}m"
                    );
                }
            }
        }
        finally
        {
            foreach (var serverLock in serverLocks)
            {
                serverLock.Release();
            }
        }

        return owned.Count;
    }

    private void KillOrphan((int ProcessId, DateTime? StartedAt) orphan)
    {
        var process = processUtilities.FindProcessById(orphan.ProcessId);
        if (process is not { HasExited: false } || StartTimeOf(process) != orphan.StartedAt)
        {
            return;
        }

        logger.LogWarning($"Process monitor: untracked arma process {orphan.ProcessId} exceeded {OrphanKillCeiling.TotalMinutes:F0}m ceiling, force-killing");
        try
        {
            processUtilities.KillProcess(process);
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }
}
