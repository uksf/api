using System.Diagnostics;
using System.ComponentModel;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager
{
    private async Task TryFinaliseKilledSessionAsync(string sessionId)
    {
        try
        {
            await missionStatsService.FinaliseKilledSessionAsync(sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to finalise killed session '{sessionId}', proceeding with server cleanup", ex);
        }

        try
        {
            await missionSessionCaptureService.CaptureEndedAsync(sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to capture mission completion for killed session '{sessionId}', proceeding with server cleanup", ex);
        }
    }

    public void EnsureMonitorRunning()
    {
        lock (_monitorLock)
        {
            if (_monitorRunning) return;
            _monitorRunning = true;
        }

        _ = Task.Run(MonitorLoop);
    }

    private static readonly TimeSpan MonitorRestartDelay = TimeSpan.FromSeconds(2);

    private async Task MonitorLoop()
    {
        var lastInstanceCount = -1;
        try
        {
            while (true)
            {
                List<DomainGameServer> serversWithProcess;
                try
                {
                    serversWithProcess = gameServersContext.Get().Where(s => s.ProcessId is not null).ToList();
                }
                catch (Exception ex)
                {
                    logger.LogError("Process monitor failed to read server state, will retry next tick", ex);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    continue;
                }

                foreach (var server in serversWithProcess)
                {
                    await CheckServer(server);
                }

                var instanceCount = await ReconcileOrphansAsync(DateTime.UtcNow);
                if (instanceCount != lastInstanceCount)
                {
                    await serversHub.Clients.All.ReceiveInstanceCount(instanceCount);
                    lastInstanceCount = instanceCount;
                }

                if (serversWithProcess.Count == 0 && instanceCount == 0)
                {
                    lock (_monitorLock)
                    {
                        if (gameServersContext.Get().All(server => server.ProcessId is null))
                        {
                            _monitorRunning = false;
                            return;
                        }
                    }

                    continue;
                }

                await Task.Delay(serversWithProcess.Count > 0 ? CalculateTickInterval(serversWithProcess) : TimeSpan.FromSeconds(2));
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Process monitor loop crashed unexpectedly, restarting", ex);
            await Task.Delay(MonitorRestartDelay);
            lock (_monitorLock)
            {
                _monitorRunning = false;
            }

            EnsureMonitorRunning();
        }
    }

    private async Task CheckServer(DomainGameServer server)
    {
        var serverLock = GetServerLock(server.Id);
        if (!await serverLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            if (server.ProcessId is null)
            {
                return;
            }

            var process = processUtilities.FindProcessById(server.ProcessId!.Value);
            if (process is not { HasExited: false } || !IsServerProcess(server, process))
            {
                await HandleProcessGone(server);
                return;
            }

            if (StopPhaseWatchdog.WatchdogExceeded(server.Status, DateTime.UtcNow))
            {
                await ForceKillServer(server, process);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error checking server '{server.Name}' (ProcessId: {server.ProcessId})", ex);
        }
        finally
        {
            serverLock.Release();
        }
    }

    private async Task ForceKillServer(DomainGameServer server, Process process)
    {
        logger.LogInfo($"Force-killing server '{server.Name}' after stop watchdog exceeded (phase {server.Status.StopPhase})");

        if (!process.HasExited && gameServerHelpers.IsOwnedProcessId(process.Id))
        {
            process.Kill(true);
        }

        await HandleProcessGone(server);
    }

    private async Task ResetServerToDeadAsync(DomainGameServer server, bool push)
    {
        var activeSessionId = server.Status.CurrentMissionSessionId;

        await Task.WhenAll(server.HeadlessClientProcessIds.Select(KillProcessAndWaitAsync));

        if (!string.IsNullOrEmpty(activeSessionId))
        {
            await TryFinaliseKilledSessionAsync(activeSessionId);
        }

        server.ProcessId = null;
        server.ProcessStartedAt = null;
        server.HeadlessClientProcessIds.Clear();
        server.LaunchedBy = null;
        server.Status = new GameServerStatus();
        StatusCache.TryRemove(server.Id, out _);

        await gameServersContext.Replace(server);

        if (push)
        {
            await PushServerUpdateAsync(server);
        }
    }

    private async Task HandleProcessGone(DomainGameServer server)
    {
        await ResetServerToDeadAsync(server, push: true);

        logger.LogInfo($"Process monitor detected server '{server.Name}' is offline");
    }

    private static TimeSpan CalculateTickInterval(List<DomainGameServer> servers)
    {
        var minInterval = TimeSpan.FromSeconds(30);
        foreach (var server in servers)
        {
            var interval = server.Status switch
            {
                { StopPhase: not StopPhase.None } => TimeSpan.FromSeconds(1),
                { Launching: true }               => TimeSpan.FromSeconds(2),
                _                                 => TimeSpan.FromSeconds(30)
            };

            if (interval < minInterval)
            {
                minInterval = interval;
            }
        }

        return minInterval;
    }
}
