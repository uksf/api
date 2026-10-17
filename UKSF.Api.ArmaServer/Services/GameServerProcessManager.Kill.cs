using System.ComponentModel;
using System.Diagnostics;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    private const string DataLossRefusal = "Killing now loses persistence data the API has not stored. Reload the page and confirm the kill";

    public async Task KillServerAsync(DomainGameServer requested, bool confirmDataLoss = false)
    {
        await WithCurrentServerAsync(
            requested.Id,
            async server =>
            {
                if (StopPhaseWatchdog.SaveNotYetStored(server.Status) && !confirmDataLoss)
                {
                    throw new BadRequestException(DataLossRefusal);
                }

                await KillServerCoreAsync(server);
                await PushServerUpdateAsync(server);
            }
        );
    }

    public async Task<int> KillAllAsync(bool confirmDataLoss = false)
    {
        var gameServers = gameServersContext.Get().OrderBy(s => s.Id).ToList();
        var locks = new List<SemaphoreSlim>();
        try
        {
            foreach (var gameServer in gameServers)
            {
                var serverLock = GetServerLock(gameServer.Id);
                await serverLock.WaitAsync();
                locks.Add(serverLock);
            }

            var current = gameServersContext.Get().OrderBy(s => s.Id).ToList();
            if (!current.Select(s => s.Id).SequenceEqual(gameServers.Select(s => s.Id)))
            {
                throw new BadRequestException("The server list changed while preparing to kill all servers. Reload the page and try again");
            }

            var saving = current.Where(s => StopPhaseWatchdog.SaveNotYetStored(s.Status)).Select(s => s.Name).ToList();
            if (saving.Count > 0 && !confirmDataLoss)
            {
                throw new BadRequestException($"{string.Join(", ", saving)} still saving. {DataLossRefusal}");
            }

            var killed = await KillOwnedArmaProcessesAsync();
            foreach (var gameServer in current)
            {
                if (!string.IsNullOrEmpty(gameServer.Status.CurrentMissionSessionId))
                {
                    await TryFinaliseKilledSessionAsync(gameServer.Status.CurrentMissionSessionId);
                }

                gameServer.ProcessId = null;
                gameServer.ProcessStartedAt = null;
                gameServer.LaunchedBy = null;
                gameServer.Status = new GameServerStatus();
                gameServer.HeadlessClients.Clear();
                StatusCache.TryRemove(gameServer.Id, out _);
                _shutdownSentAt.TryRemove(gameServer.Id, out _);
                await gameServersContext.Replace(gameServer);
            }

            return killed;
        }
        finally
        {
            locks.ForEach(serverLock => serverLock.Release());
        }
    }

    private async Task<int> KillOwnedArmaProcessesAsync()
    {
        var processes = gameServerHelpers.GetGameServerArmaProcesses()
                                         .Select(listed => TryOpenOwnedProcess(Track(listed.ProcessId)))
                                         .Where(process => process is not null)
                                         .ToList();
        await Task.WhenAll(processes.Select(KillProcessAndWaitAsync));
        return processes.Count;
    }

    private async Task KillServerCoreAsync(DomainGameServer server)
    {
        if (server.ProcessId is { } processId)
        {
            if (TryOpenOwnedProcess(ServerProcessOf(server)) is { } process)
            {
                await KillProcessAndWaitAsync(process);
            }
            else
            {
                logger.LogWarning($"Not killing PID {processId} for '{server.Name}': it is gone, not owned by this API, or a different process reused the PID");
            }
        }

        await ResetServerToDeadAsync(server, push: false);
    }

    private async Task KillProcessAndWaitAsync(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            processUtilities.KillProcess(process);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException) { }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }
}
