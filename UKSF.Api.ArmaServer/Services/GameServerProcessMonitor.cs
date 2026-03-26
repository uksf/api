using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServerProcessMonitor
{
    void EnsureRunning();
}

public class GameServerProcessMonitor(
    IServiceScopeFactory serviceScopeFactory,
    IProcessUtilities processUtilities,
    IHubContext<ServersHub, IServersClient> serversHub,
    IUksfLogger logger
) : IGameServerProcessMonitor
{
    private readonly Lock _lock = new();
    private bool _running;

    public void EnsureRunning()
    {
        lock (_lock)
        {
            if (_running) return;
            _running = true;
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
                    using var scope = serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<IGameServersContext>();
                    serversWithProcess = context.Get().Where(s => s.ProcessId is not null).ToList();
                }
                catch (Exception ex)
                {
                    logger.LogError("Process monitor failed to read server state, will retry next tick", ex);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    continue;
                }

                if (serversWithProcess.Count == 0)
                {
                    break;
                }

                foreach (var server in serversWithProcess)
                {
                    await CheckServer(server);
                }

                var interval = CalculateTickInterval(serversWithProcess);
                await Task.Delay(interval);
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Process monitor loop crashed unexpectedly", ex);
        }
        finally
        {
            lock (_lock)
            {
                _running = false;
            }
        }
    }

    private async Task CheckServer(DomainGameServer server)
    {
        try
        {
            if (server.Status.Stopping &&
                server.Status.StoppingInitiatedAt.HasValue &&
                DateTime.UtcNow - server.Status.StoppingInitiatedAt.Value > TimeSpan.FromSeconds(30))
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
    }

    private async Task ForceKillServer(DomainGameServer server)
    {
        logger.LogInfo($"Force-killing server '{server.Name}' after 30s stopping timeout");

        var process = processUtilities.FindProcessById(server.ProcessId!.Value);
        if (process is { HasExited: false })
        {
            process.Kill(true);
        }

        await HandleProcessGone(server);
    }

    private async Task HandleProcessGone(DomainGameServer server)
    {
        var activeSessionId = server.Status.CurrentMissionSessionId;

        foreach (var hcProcessId in server.HeadlessClientProcessIds)
        {
            var hcProcess = processUtilities.FindProcessById(hcProcessId);
            if (hcProcess is { HasExited: false })
            {
                hcProcess.Kill(true);
                try
                {
                    await hcProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException) { }
                catch (InvalidOperationException) { }
            }
        }

        using var scope = serviceScopeFactory.CreateScope();

        if (!string.IsNullOrEmpty(activeSessionId))
        {
            var missionStatsService = scope.ServiceProvider.GetRequiredService<IMissionStatsService>();
            await missionStatsService.FinaliseKilledSessionAsync(activeSessionId);
        }

        server.ProcessId = null;
        server.HeadlessClientProcessIds.Clear();
        server.LaunchedBy = null;
        server.Status = new GameServerStatus();

        var context = scope.ServiceProvider.GetRequiredService<IGameServersContext>();
        await context.Replace(server);

        var gameServersService = scope.ServiceProvider.GetRequiredService<IGameServersService>();
        gameServersService.ClearStatusCache(server.Id);

        await serversHub.Clients.All.ReceiveServerUpdate(new GameServerUpdate { Server = server, InstanceCount = gameServersService.GetGameInstanceCount() });

        logger.LogInfo($"Process monitor detected server '{server.Name}' is offline");
    }

    private static TimeSpan CalculateTickInterval(List<DomainGameServer> servers)
    {
        var minInterval = TimeSpan.FromSeconds(30);
        foreach (var server in servers)
        {
            var interval = server.Status switch
            {
                { Stopping: true }  => TimeSpan.FromSeconds(1),
                { Launching: true } => TimeSpan.FromSeconds(2),
                _                   => TimeSpan.FromSeconds(30)
            };

            if (interval < minInterval)
            {
                minInterval = interval;
            }
        }

        return minInterval;
    }
}

public class GameServerProcessMonitorStartup(IServiceScopeFactory serviceScopeFactory, IGameServerProcessMonitor processMonitor) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IGameServersContext>();
        if (context.Get().Any(s => s.ProcessId is not null))
        {
            processMonitor.EnsureRunning();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
