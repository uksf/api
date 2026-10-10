using System.ComponentModel;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    private readonly Dictionary<int, DateTime> _untrackedSince = [];

    internal void KillLongUntrackedProcesses(IReadOnlyCollection<DomainGameServer> trackedServers, DateTime nowUtc)
    {
        var trackedIds = trackedServers.SelectMany(server => server.HeadlessClientProcessIds.Append(server.ProcessId ?? 0)).ToHashSet();
        var untrackedIds = gameServerHelpers.GetGameServerArmaProcesses()
                                            .Select(process => process.ProcessId)
                                            .Where(id => !trackedIds.Contains(id))
                                            .ToHashSet();

        foreach (var exited in _untrackedSince.Keys.Where(id => !untrackedIds.Contains(id)).ToList())
        {
            _untrackedSince.Remove(exited);
        }

        foreach (var id in untrackedIds)
        {
            var since = _untrackedSince.TryAdd(id, nowUtc) ? nowUtc : _untrackedSince[id];
            if (nowUtc - since < OrphanKillCeiling)
            {
                continue;
            }

            logger.LogWarning(
                $"Process monitor: untracked arma process {id} exceeded {OrphanKillCeiling.TotalMinutes:F0}m ceiling while servers are tracked, force-killing"
            );
            _untrackedSince.Remove(id);
            var process = processUtilities.FindProcessById(id);
            if (process is { HasExited: false })
            {
                try
                {
                    processUtilities.KillProcess(process);
                }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
            }
        }
    }
}
