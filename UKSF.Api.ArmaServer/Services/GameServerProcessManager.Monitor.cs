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

    private async Task MonitorLoop()
    {
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

                if (serversWithProcess.Count > 0)
                {
                    foreach (var server in serversWithProcess)
                    {
                        await CheckServer(server);
                    }

                    var interval = CalculateTickInterval(serversWithProcess);
                    await Task.Delay(interval);
                    continue;
                }

                var remainingProcesses = GetInstanceCount();
                if (remainingProcesses == 0)
                {
                    await serversHub.Clients.All.ReceiveInstanceCount(0);
                    break;
                }

                var orphanStart = DateTime.UtcNow;
                var lastReportedCount = remainingProcesses;
                var lastLogAt = orphanStart;
                var forceKilled = false;
                logger.LogInfo($"Process monitor: {remainingProcesses} orphaned arma process(es) still running after server state cleared, waiting for exit");

                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));

                    var currentCount = GetInstanceCount();
                    if (currentCount != lastReportedCount)
                    {
                        await serversHub.Clients.All.ReceiveInstanceCount(currentCount);
                        lastReportedCount = currentCount;
                    }

                    if (currentCount == 0)
                    {
                        logger.LogInfo($"Process monitor: orphaned arma process(es) exited after {(DateTime.UtcNow - orphanStart).TotalSeconds:F0}s");
                        break;
                    }

                    if (!forceKilled && DateTime.UtcNow - orphanStart >= OrphanKillCeiling)
                    {
                        logger.LogWarning(
                            $"Process monitor: {currentCount} orphaned arma process(es) exceeded {OrphanKillCeiling.TotalMinutes:F0}m ceiling, force-killing"
                        );
                        KillOrphanedArmaProcesses();
                        forceKilled = true;
                    }

                    if (DateTime.UtcNow - lastLogAt >= TimeSpan.FromSeconds(30))
                    {
                        logger.LogWarning(
                            $"Process monitor: {currentCount} orphaned arma process(es) still running after {(DateTime.UtcNow - orphanStart).TotalSeconds:F0}s"
                        );
                        lastLogAt = DateTime.UtcNow;
                    }
                }

                if (gameServersContext.Get().Any(s => s.ProcessId is not null))
                {
                    continue; // a server was launched during the drain; reconcile it instead of exiting
                }

                break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Process monitor loop crashed unexpectedly", ex);
        }
        finally
        {
            lock (_monitorLock)
            {
                _monitorRunning = false;
            }
        }
    }

    internal void KillOrphanedArmaProcesses()
    {
        foreach (var processInfo in gameServerHelpers.GetGameServerArmaProcesses())
        {
            var process = processUtilities.FindProcessById(processInfo.ProcessId);
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

            if (StopWatchdogExceeded(server.Status, DateTime.UtcNow))
            {
                await ForceKillServer(server);
                return;
            }

            var process = processUtilities.FindProcessById(server.ProcessId!.Value);
            if (process is { HasExited: false })
            {
                return;
            }

            await HandleProcessGone(server);
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

    private async Task ForceKillServer(DomainGameServer server)
    {
        logger.LogInfo($"Force-killing server '{server.Name}' after stop watchdog exceeded (phase {server.Status.StopPhase})");

        var process = processUtilities.FindProcessById(server.ProcessId!.Value);
        if (process is { HasExited: false })
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

    public static bool StopWatchdogExceeded(GameServerStatus status, DateTime nowUtc)
    {
        if (status.StopPhase == StopPhase.None)
        {
            return false;
        }

        if (status.StopPhaseEnteredAt is { } enteredAt)
        {
            var ceiling = status.StopPhase switch
            {
                StopPhase.Ending   => EndingCeiling,
                StopPhase.Saving   => SavingCeiling,
                StopPhase.Stopping => StoppingCeiling,
                _                  => StopBackstopCeiling
            };
            return nowUtc - enteredAt > ceiling;
        }

        return status.StopRequestedAt is { } requestedAt && nowUtc - requestedAt > StopBackstopCeiling;
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
                _                                  => TimeSpan.FromSeconds(30)
            };

            if (interval < minInterval)
            {
                minInterval = interval;
            }
        }

        return minInterval;
    }
}
