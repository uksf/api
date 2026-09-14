using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public partial class GameServerProcessManager(
    IGameServersContext gameServersContext,
    IGameServerHelpers gameServerHelpers,
    IProcessUtilities processUtilities,
    IHttpClientFactory httpClientFactory,
    IHubContext<ServersHub, IServersClient> serversHub,
    IMissionsService missionsService,
    IRptLogService rptLogService,
    IMissionStatsService missionStatsService,
    IMissionSessionCaptureService missionSessionCaptureService,
    IVariablesService variablesService,
    IUksfLogger logger
) : IGameServerProcessManager
{
    private readonly ConcurrentDictionary<string, GameServerStatus> StatusCache = new();

    // Two independent force-kill ceilings, kept separate on purpose:
    // OrphanKillCeiling catches a process that died or hung outside any stop; the per-stage
    // stop watchdog (Ending/Saving/Stopping/StopBackstop) bounds a graceful stop's lifecycle.
    // Do not merge them (see commit 364f5cc2, hung-engine orphan incident).
    private static readonly TimeSpan OrphanKillCeiling = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _serverLocks = new();
    private readonly Lock _monitorLock = new();
    private bool _monitorRunning;

    // Guards one DomainGameServer instance per Id only because gameServersContext is a
    // CachedMongoContext: with USE_MEMORY_DATA_CACHE on, Get()/GetSingle return the same
    // shared instance every call. If that cache is ever disabled, this lock guards unrelated
    // per-call instances instead -> lost updates / double-finalise.
    private SemaphoreSlim GetServerLock(string serverId)
    {
        return _serverLocks.GetOrAdd(serverId, _ => new SemaphoreSlim(1, 1));
    }

    public int GetInstanceCount()
    {
        return gameServerHelpers.GetGameServerArmaProcesses().Count;
    }

    public async Task PushServerUpdateAsync(DomainGameServer server)
    {
        server.LogSources = rptLogService.GetLogSources(server);
        var update = new GameServerUpdate { Server = server, InstanceCount = GetInstanceCount() };
        await serversHub.Clients.All.ReceiveServerUpdate(update);
    }

    public async Task PushAllServersUpdateAsync()
    {
        var servers = gameServersContext.Get().ToList();
        foreach (var server in servers)
        {
            server.LogSources = rptLogService.GetLogSources(server);
        }

        var update = new GameServersUpdate
        {
            Servers = servers,
            Missions = missionsService.GetActiveMissions(),
            InstanceCount = GetInstanceCount()
        };
        await serversHub.Clients.All.ReceiveServersUpdate(update);
    }

    public async Task LaunchServerAsync(DomainGameServer server, string missionName, string launchedBy, int playerCount)
    {
        var serverLock = GetServerLock(server.Id);
        await serverLock.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(
                gameServerHelpers.GetGameServerConfigPath(server),
                gameServerHelpers.FormatGameServerConfig(server, playerCount, missionName)
            );

            server.Status = new GameServerStatus { Launching = true };
            server.HeadlessClientProcessIds.Clear(); // defensive: don't accumulate onto a stale list from a prior run
            StatusCache.TryRemove(server.Id, out _);

            if (missionName is not null)
            {
                var fileName = Path.GetFileName(missionName);
                var nameWithoutExtension = fileName.EndsWith(".pbo", StringComparison.OrdinalIgnoreCase) ? fileName[..^4] : fileName;
                var lastDot = nameWithoutExtension.LastIndexOf('.');
                if (lastDot > 0)
                {
                    server.Status.Mission = nameWithoutExtension[..lastDot];
                    server.Status.Map = nameWithoutExtension[(lastDot + 1)..];
                }
                else
                {
                    server.Status.Mission = nameWithoutExtension;
                }
            }

            if (launchedBy is not null)
            {
                server.LaunchedBy = launchedBy;
            }

            var launchArguments = gameServerHelpers.FormatGameServerLaunchArguments(server);
            server.ProcessId = processUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(server), launchArguments);

            await Task.Delay(TimeSpan.FromMilliseconds(50));

            for (var index = 0; index < server.NumberHeadlessClients; index++)
            {
                launchArguments = gameServerHelpers.FormatHeadlessClientLaunchArguments(server, index);
                server.HeadlessClientProcessIds.Add(
                    processUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(server), launchArguments)
                );

                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            await gameServersContext.Replace(server);
            await PushServerUpdateAsync(server);
        }
        finally
        {
            serverLock.Release();
        }

        EnsureMonitorRunning();
    }

    public async Task StopServerAsync(DomainGameServer server)
    {
        var serverLock = GetServerLock(server.Id);
        await serverLock.WaitAsync();
        try
        {
            if (!server.Status.Running)
            {
                await KillServerCoreAsync(server);
                await PushServerUpdateAsync(server);
                return;
            }

            server.Status.StopPhase = StopPhase.Ending;
            server.Status.StopRequestedAt = DateTime.UtcNow;
            server.Status.StopPhaseEnteredAt = null; // provisional, armed only by a game shutdown event
            server.Status.KillAllowedAt = StopPhaseWatchdog.KillOfferAt(server.Status);
            await gameServersContext.Replace(server);
            await SendShutdownAsync(server.ApiPort, $"game server '{server.Name}'");
            await PushServerUpdateAsync(server);
        }
        finally
        {
            serverLock.Release();
        }

        EnsureMonitorRunning();
    }

    public async Task KillServerAsync(DomainGameServer server)
    {
        var serverLock = GetServerLock(server.Id);
        await serverLock.WaitAsync();
        try
        {
            await KillServerCoreAsync(server);
            await PushServerUpdateAsync(server);
        }
        finally
        {
            serverLock.Release();
        }
    }

    public async Task<int> KillAllAsync()
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

        var gameServers = gameServersContext.Get().OrderBy(s => s.Id).ToList();

        foreach (var gameServer in gameServers)
        {
            var serverLock = GetServerLock(gameServer.Id);
            await serverLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(gameServer.Status.CurrentMissionSessionId))
                {
                    await TryFinaliseKilledSessionAsync(gameServer.Status.CurrentMissionSessionId);
                }

                gameServer.ProcessId = null;
                gameServer.LaunchedBy = null;
                gameServer.Status = new GameServerStatus();
                gameServer.HeadlessClientProcessIds.Clear();
                StatusCache.TryRemove(gameServer.Id, out _);
                await gameServersContext.Replace(gameServer);
            }
            finally
            {
                serverLock.Release();
            }
        }

        return processes.Count;
    }

    private async Task KillServerCoreAsync(DomainGameServer server)
    {
        if (server.ProcessId is not null)
        {
            await KillProcessAndWaitAsync(server.ProcessId.Value);
        }

        await ResetServerToDeadAsync(server, push: false);
    }

    private async Task KillProcessAndWaitAsync(int processId)
    {
        var process = processUtilities.FindProcessById(processId);
        if (process is { HasExited: false })
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

    private async Task SendShutdownAsync(int port, string context)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5); // this call runs under the per-server lock; don't block it on a hung endpoint
            // Game-side handleCommand expects an SQF array envelope; the extension
            // forwards the body to the game callback verbatim.
            var content = new StringContent("[\"shutdown\"]", System.Text.Encoding.UTF8, "text/plain");
            await client.PostAsync($"http://127.0.0.1:{port}/command", content);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning($"HTTP request failed while stopping {context}: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning($"Request timed out while stopping {context}: {ex.Message}");
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error stopping {context}", exception);
        }
    }
}
