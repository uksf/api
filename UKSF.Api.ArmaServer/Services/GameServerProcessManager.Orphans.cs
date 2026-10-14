using System.ComponentModel;
using UKSF.Api.ArmaServer.Models;

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

        var lockedServerIds = gameServersContext.Get().Select(server => server.Id).Distinct().Order().ToList();
        var serverLocks = lockedServerIds.Select(GetServerLock).ToList();
        foreach (var serverLock in serverLocks)
        {
            await serverLock.WaitAsync();
        }

        try
        {
            var servers = gameServersContext.Get();
            if (!servers.Select(server => server.Id).Distinct().Order().SequenceEqual(lockedServerIds))
            {
                return owned.Count;
            }

            var tracked = servers.SelectMany(server => server.HeadlessClients.Append(server.ServerProcess)).Where(process => process is not null).ToHashSet();
            var trackedWithoutStartTime = servers.Where(server => server.ProcessStartedAt is null && server.ProcessId is not null)
                                                 .Select(server => server.ProcessId.Value)
                                                 .ToHashSet();
            var orphans = owned.Where(process => !trackedWithoutStartTime.Contains(process.ProcessId))
                               .Select(process => (process.ProcessId, StartedAt: Track(process.ProcessId)?.StartedAt))
                               .Where(process => process.StartedAt is not { } startedAt || !tracked.Contains(new TrackedProcess(process.ProcessId, startedAt)))
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
        if (orphan.StartedAt is not { } startedAt || TryOpenOwnedProcess(new TrackedProcess(orphan.ProcessId, startedAt)) is not { } process)
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
