using System.ComponentModel;
using System.Diagnostics;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    private const string DataLossRefusal = "Killing now loses persistence data the API has not stored. Reload the page and confirm the kill";

    public async Task KillServerAsync(DomainGameServer server, bool confirmDataLoss = false)
    {
        var serverLock = GetServerLock(server.Id);
        await serverLock.WaitAsync();
        try
        {
            var current = gameServersContext.GetSingle(server.Id) ?? server;
            if (StopPhaseWatchdog.SaveNotYetStored(current.Status.StopPhase) && !confirmDataLoss)
            {
                throw new BadRequestException(DataLossRefusal);
            }

            await KillServerCoreAsync(current);
            await PushServerUpdateAsync(current);
        }
        finally
        {
            serverLock.Release();
        }
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

            var saving = current.Where(s => StopPhaseWatchdog.SaveNotYetStored(s.Status.StopPhase)).Select(s => s.Name).ToList();
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
                gameServer.HeadlessClientProcessIds.Clear();
                StatusCache.TryRemove(gameServer.Id, out _);
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
                                         .Select(p => processUtilities.FindProcessById(p.ProcessId))
                                         .Where(p => p is not null)
                                         .ToList();
        foreach (var process in processes)
        {
            try
            {
                processUtilities.KillProcess(process);
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        }

        await Task.WhenAll(
            processes.Select(async process =>
                {
                    try
                    {
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException) { }
                    catch (InvalidOperationException) { }
                }
            )
        );

        return processes.Count;
    }

    private async Task KillServerCoreAsync(DomainGameServer server)
    {
        if (server.ProcessId is { } processId)
        {
            var process = processUtilities.FindProcessById(processId);
            if (process is not null && IsServerProcess(server, process))
            {
                await KillProcessAndWaitAsync(process);
            }
            else if (process is not null)
            {
                logger.LogWarning($"Not killing PID {processId} for '{server.Name}': the process started at a different time, so the PID was reused");
            }
        }

        await ResetServerToDeadAsync(server, push: false);
    }

    private async Task KillProcessAndWaitAsync(int processId)
    {
        if (!gameServerHelpers.IsOwnedProcessId(processId))
        {
            return;
        }

        var process = processUtilities.FindProcessById(processId);
        if (process is not null)
        {
            await KillProcessAndWaitAsync(process);
        }
    }

    private async Task KillProcessAndWaitAsync(Process process)
    {
        if (!gameServerHelpers.IsOwnedProcessId(process.Id))
        {
            return;
        }

        if (!process.HasExited)
        {
            try
            {
                process.Kill(true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException) { }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        }
    }
}
