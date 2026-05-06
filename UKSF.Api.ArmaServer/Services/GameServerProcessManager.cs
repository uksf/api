using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Parsing;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServerProcessManager
{
    Task LaunchServerAsync(DomainGameServer server, string missionName, string launchedBy, int playerCount);
    Task StopServerAsync(DomainGameServer server);
    Task KillServerAsync(DomainGameServer server);
    Task<int> KillAllAsync();
    int GetInstanceCount();
    Task<List<DomainGameServer>> GetAllServerStatusesAsync();
    Task HandleShutdownCompleteAsync(int apiPort);
    Task HandleServerStatusAsync(int apiPort, Dictionary<string, object> data);
    Task PushServerUpdateAsync(DomainGameServer server);
    Task PushAllServersUpdateAsync();
    void EnsureMonitorRunning();
}

public class GameServerProcessManager(
    IGameServersContext gameServersContext,
    IGameServerHelpers gameServerHelpers,
    IProcessUtilities processUtilities,
    IHttpClientFactory httpClientFactory,
    IHubContext<ServersHub, IServersClient> serversHub,
    IMissionsService missionsService,
    IRptLogService rptLogService,
    IMissionStatsService missionStatsService,
    IVariablesService variablesService,
    IUksfLogger logger
) : IGameServerProcessManager
{
    private static readonly ConcurrentDictionary<string, GameServerStatus> StatusCache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _serverLocks = new();
    private readonly Lock _monitorLock = new();
    private bool _monitorRunning;

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
            File.WriteAllText(gameServerHelpers.GetGameServerConfigPath(server), gameServerHelpers.FormatGameServerConfig(server, playerCount, missionName));

            server.Status = new GameServerStatus { Launching = true };
            StatusCache.TryRemove(server.Id, out _);

            if (missionName is not null)
            {
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(missionName);
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

            server.Status.Stopping = true;
            server.Status.StoppingInitiatedAt = DateTime.UtcNow;
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
            process.Kill(true);
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
                    try
                    {
                        await missionStatsService.FinaliseKilledSessionAsync(gameServer.Status.CurrentMissionSessionId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to finalise killed session '{gameServer.Status.CurrentMissionSessionId}', proceeding with server cleanup", ex);
                    }
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
        var activeSessionId = server.Status.CurrentMissionSessionId;

        if (server.ProcessId is not null)
        {
            var process = processUtilities.FindProcessById(server.ProcessId.Value);
            if (process is { HasExited: false })
            {
                process.Kill(true);
                try
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException) { }
                catch (InvalidOperationException) { }
            }
        }

        if (!string.IsNullOrEmpty(activeSessionId))
        {
            try
            {
                await missionStatsService.FinaliseKilledSessionAsync(activeSessionId);
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to finalise killed session '{activeSessionId}', proceeding with server cleanup", ex);
            }
        }

        server.ProcessId = null;
        server.LaunchedBy = null;
        server.Status = new GameServerStatus();
        StatusCache.TryRemove(server.Id, out _);

        await Task.WhenAll(
            server.HeadlessClientProcessIds.Select(async hcProcessId =>
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
            )
        );
        server.HeadlessClientProcessIds.Clear();

        await gameServersContext.Replace(server);
    }

    private async Task SendShutdownAsync(int port, string context)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            // Game-side handleCommand expects an SQF array envelope; the extension
            // forwards the body to the game callback verbatim.
            var content = new StringContent("[\"shutdown\"]", System.Text.Encoding.UTF8, "text/plain");
            await client.PostAsync($"http://localhost:{port}/command", content);
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

    public async Task<List<DomainGameServer>> GetAllServerStatusesAsync()
    {
        var gameServers = gameServersContext.Get().ToList();

        if (variablesService.GetFeatureState("SKIP_SERVER_STATUS"))
        {
            foreach (var gameServer in gameServers)
            {
                await gameServersContext.Replace(gameServer);
            }

            return gameServers;
        }

        if (!gameServerHelpers.GetGameServerArmaProcesses().Any())
        {
            StatusCache.Clear();

            foreach (var gameServer in gameServers)
            {
                if (!string.IsNullOrEmpty(gameServer.Status.CurrentMissionSessionId))
                {
                    await TryFinaliseKilledSessionAsync(gameServer.Status.CurrentMissionSessionId);
                }
            }

            foreach (var gameServer in gameServers)
            {
                gameServer.Status = new GameServerStatus();
                gameServer.ProcessId = null;
                gameServer.HeadlessClientProcessIds.Clear();
                await gameServersContext.Replace(gameServer);
            }

            return gameServers;
        }

        var armaProcesses = gameServerHelpers.GetGameServerArmaProcesses();
        await Task.WhenAll(gameServers.Select(server => UpdateServerStatus(server, armaProcesses)));
        return gameServers;
    }

    public async Task HandleShutdownCompleteAsync(int apiPort)
    {
        var gameServer = gameServersContext.GetSingle(x => x.ApiPort == apiPort);
        if (gameServer is null)
        {
            logger.LogWarning($"Received shutdown_complete but no server matches apiPort {apiPort}");
            return;
        }

        var serverLock = GetServerLock(gameServer.Id);
        await serverLock.WaitAsync();
        try
        {
            foreach (var processId in gameServer.HeadlessClientProcessIds)
            {
                var process = processUtilities.FindProcessById(processId);
                if (process is { HasExited: false })
                {
                    process.Kill(true);
                    try
                    {
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (TimeoutException) { }
                    catch (InvalidOperationException) { }
                }
            }

            var activeSessionId = gameServer.Status.CurrentMissionSessionId;
            if (!string.IsNullOrEmpty(activeSessionId))
            {
                await TryFinaliseKilledSessionAsync(activeSessionId);
            }

            gameServer.ProcessId = null;
            gameServer.LaunchedBy = null;
            gameServer.HeadlessClientProcessIds.Clear();
            gameServer.Status = new GameServerStatus();
            StatusCache.TryRemove(gameServer.Id, out _);
            await gameServersContext.Replace(gameServer);

            await PushServerUpdateAsync(gameServer);
            logger.LogInfo($"Server shutdown complete: {gameServer.Name} (apiPort {gameServer.ApiPort})");
        }
        finally
        {
            serverLock.Release();
        }
    }

    public async Task HandleServerStatusAsync(int apiPort, Dictionary<string, object> data)
    {
        var gameServer = gameServersContext.GetSingle(x => x.ApiPort == apiPort);
        if (gameServer is null)
        {
            logger.LogWarning($"Received server_status but no server matches apiPort {apiPort}");
            return;
        }

        var serverLock = GetServerLock(gameServer.Id);
        await serverLock.WaitAsync();
        try
        {
            var status = StatusCache.GetOrAdd(gameServer.Id, _ => gameServer.Status ?? new GameServerStatus());

            if (data.TryGetValue("map", out var map)) status.Map = map.ToString();
            if (data.TryGetValue("mission", out var mission)) status.Mission = mission.ToString();
            if (data.TryGetValue("players", out var players) && players is List<object> playersList)
            {
                status.Players = playersList.OfType<string>().ToList();
            }
            else
            {
                logger.LogWarning($"server_status 'players' missing or not a list. Keys: {string.Join(", ", data.Keys)}");
            }

            if (data.TryGetValue("uptime", out var uptime) &&
                float.TryParse(uptime.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var uptimeValue))
            {
                status.Uptime = uptimeValue;
                status.ParsedUptime = gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(uptimeValue)).ToString();
                status.StartedAt ??= DateTime.UtcNow.AddSeconds(-uptimeValue);
            }

            if (data.TryGetValue("entityCount", out var entityCount) && int.TryParse(entityCount.ToString(), out var entityCountValue))
                status.EntityCount = entityCountValue;
            if (data.TryGetValue("aiCount", out var aiCount) && int.TryParse(aiCount.ToString(), out var aiCountValue)) status.AiCount = aiCountValue;
            if (data.TryGetValue("headlessClientCount", out var headlessClientCount) &&
                int.TryParse(headlessClientCount.ToString(), out var headlessClientCountValue)) status.HeadlessClientCount = headlessClientCountValue;

            status.Running = true;
            status.Launching = false;
            status.MaxPlayers = gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
            status.LastEventReceived = DateTime.UtcNow;

            gameServer.Status = status;
            await gameServersContext.Replace(gameServer);

            await PushServerUpdateAsync(gameServer);
        }
        finally
        {
            serverLock.Release();
        }
    }

    private void ApplyPolledStatus(DomainGameServer gameServer, string sqfBody)
    {
        // Body is engine-native SQF str() of the server_status data hashmap (pair-list).
        // Parse to a Dictionary<string,object> then map fields onto the in-memory status.
        Dictionary<string, object> polled;
        try
        {
            polled = ToDict(SqfNotationParser.ParseAndNormalize(sqfBody));
        }
        catch (FormatException exception)
        {
            logger.LogWarning($"Failed to parse polled server status SQF for '{gameServer.Name}': {exception.Message}");
            return;
        }

        if (polled.TryGetValue("map", out var map)) gameServer.Status.Map = map?.ToString();
        if (polled.TryGetValue("mission", out var mission)) gameServer.Status.Mission = mission?.ToString();
        if (polled.TryGetValue("players", out var players) && players is List<object> playersList)
        {
            gameServer.Status.Players = playersList.OfType<string>().ToList();
        }

        if (polled.TryGetValue("uptime", out var uptime) &&
            float.TryParse(uptime?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var uptimeValue))
        {
            gameServer.Status.Uptime = uptimeValue;
            gameServer.Status.ParsedUptime = gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(uptimeValue)).ToString();
        }

        if (polled.TryGetValue("entityCount", out var entityCount) && int.TryParse(entityCount?.ToString(), out var entityCountValue))
            gameServer.Status.EntityCount = entityCountValue;
        if (polled.TryGetValue("aiCount", out var aiCount) && int.TryParse(aiCount?.ToString(), out var aiCountValue)) gameServer.Status.AiCount = aiCountValue;
        if (polled.TryGetValue("headlessClientCount", out var headlessClientCount) &&
            int.TryParse(headlessClientCount?.ToString(), out var headlessClientCountValue)) gameServer.Status.HeadlessClientCount = headlessClientCountValue;

        gameServer.Status.MaxPlayers = gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
        gameServer.Status.Running = true;
        gameServer.Status.Launching = false;
    }

    private async Task UpdateServerStatus(DomainGameServer gameServer, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        var matchingProcess = FindMatchingServerProcess(gameServer, armaProcesses);
        UpdateHeadlessClientProcessIds(gameServer, armaProcesses);

        if (matchingProcess is null)
        {
            var sessionId = gameServer.Status.CurrentMissionSessionId;
            if (!string.IsNullOrEmpty(sessionId))
            {
                await TryFinaliseKilledSessionAsync(sessionId);
            }

            gameServer.Status = new GameServerStatus();
            gameServer.ProcessId = null;
            StatusCache.TryRemove(gameServer.Id, out _);
            await gameServersContext.Replace(gameServer);
            return;
        }

        gameServer.ProcessId = matchingProcess.ProcessId;
        gameServer.Status.Launching = true;

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        client.Timeout = TimeSpan.FromSeconds(5);
        try
        {
            var response = await client.GetAsync($"http://localhost:{gameServer.ApiPort}/server");
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                if (statusCode is 502 or 504)
                {
                    gameServer.Status.Running = false;
                }
                else if (statusCode == 503) { }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    logger.LogWarning($"Status endpoint returned {statusCode} for '{gameServer.Name}': {body}");
                }
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                ApplyPolledStatus(gameServer, content);
            }
        }
        catch (HttpRequestException)
        {
            gameServer.Status.Running = false;
        }
        catch (TaskCanceledException)
        {
            gameServer.Status.Running = false;
        }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error getting game server status for '{gameServer.Name}'", exception);
            gameServer.Status.Running = false;
        }

        if (StatusCache.TryGetValue(gameServer.Id, out var cachedStatus) && cachedStatus.LastEventReceived > DateTime.UtcNow.AddSeconds(-30))
        {
            gameServer.Status.Map = cachedStatus.Map;
            gameServer.Status.Mission = cachedStatus.Mission;
            gameServer.Status.Players = cachedStatus.Players;
            gameServer.Status.Uptime = cachedStatus.Uptime;
            gameServer.Status.ParsedUptime = cachedStatus.ParsedUptime;
            gameServer.Status.StartedAt = cachedStatus.StartedAt;
            gameServer.Status.EntityCount = cachedStatus.EntityCount;
            gameServer.Status.AiCount = cachedStatus.AiCount;
            gameServer.Status.HeadlessClientCount = cachedStatus.HeadlessClientCount;
            gameServer.Status.MaxPlayers = cachedStatus.MaxPlayers;
            gameServer.Status.Running = cachedStatus.Running;
            gameServer.Status.Launching = cachedStatus.Launching;
            gameServer.Status.LastEventReceived = cachedStatus.LastEventReceived;
        }

        await gameServersContext.Replace(gameServer);
    }

    private static ProcessCommandLineInfo FindMatchingServerProcess(DomainGameServer gameServer, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        return armaProcesses.FirstOrDefault(p => MatchesPort(p, gameServer.Port) && p.CommandLine.Contains("-config=") && !p.CommandLine.Contains("-client"));
    }

    private static void UpdateHeadlessClientProcessIds(DomainGameServer gameServer, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        gameServer.HeadlessClientProcessIds = armaProcesses.Where(p => MatchesPort(p, gameServer.Port) && p.CommandLine.Contains("-client"))
                                                           .Select(p => p.ProcessId)
                                                           .ToList();
    }

    private static bool MatchesPort(ProcessCommandLineInfo process, int port)
    {
        var portArg = $"-port={port} ";
        var portArgEnd = $"-port={port}";
        return process.CommandLine.Contains(portArg) || process.CommandLine.EndsWith(portArgEnd);
    }

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

                    if (DateTime.UtcNow - lastLogAt >= TimeSpan.FromSeconds(30))
                    {
                        logger.LogWarning(
                            $"Process monitor: {currentCount} orphaned arma process(es) still running after {(DateTime.UtcNow - orphanStart).TotalSeconds:F0}s"
                        );
                        lastLogAt = DateTime.UtcNow;
                    }
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

            if (server.Status.Stopping &&
                server.Status.StoppingInitiatedAt.HasValue &&
                DateTime.UtcNow - server.Status.StoppingInitiatedAt.Value > TimeSpan.FromSeconds(60))
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
        logger.LogInfo($"Force-killing server '{server.Name}' after 60s stopping timeout");

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

        await PushServerUpdateAsync(server);

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

public class GameServerProcessManagerStartup(IGameServersContext gameServersContext, IGameServerProcessManager processManager) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (gameServersContext.Get().Any(s => s.ProcessId is not null))
        {
            processManager.EnsureMonitorRunning();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
