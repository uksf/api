using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Consumers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Request;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServersService
{
    int GetGameInstanceCount();
    Task<List<DomainGameServer>> GetAllGameServerStatuses();
    void WriteServerConfig(DomainGameServer gameServer, int playerCount, string missionSelection);
    Task LaunchGameServer(DomainGameServer gameServer, string missionName = null, string launchedBy = null);
    Task StopGameServer(DomainGameServer gameServer);
    Task KillGameServer(DomainGameServer gameServer);
    Task<int> KillAllArmaProcesses();
    List<GameServerMod> GetAvailableMods(string id);
    List<GameServerMod> GetEnvironmentMods(GameEnvironment environment);
    Task UpdateGameServerOrder(OrderUpdateRequest orderUpdate);
    Task HandleGameServerEvent(GameServerEvent gameServerEvent);
    void ClearStatusCache(string serverId);
}

public class GameServersService(
    IGameServersContext gameServersContext,
    IGameServerHelpers gameServerHelpers,
    IProcessUtilities processUtilities,
    IHttpClientFactory httpClientFactory,
    IVariablesService variablesService,
    IHubContext<ServersHub, IServersClient> serversHub,
    IUksfLogger logger,
    IPublishEndpoint publishEndpoint,
    IPersistenceSessionsService persistenceSessionsService,
    IMissionStatsService missionStatsService
) : IGameServersService
{
    private static readonly ConcurrentDictionary<string, GameServerStatus> StatusCache = new();

    public int GetGameInstanceCount()
    {
        return gameServerHelpers.GetArmaProcesses().Count();
    }

    public async Task<List<DomainGameServer>> GetAllGameServerStatuses()
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

        if (!gameServerHelpers.GetArmaProcesses().Any())
        {
            StatusCache.Clear();
            foreach (var gameServer in gameServers)
            {
                gameServer.Status = new GameServerStatus();
                gameServer.ProcessId = null;
                gameServer.HeadlessClientProcessIds.Clear();
                await gameServersContext.Replace(gameServer);
            }

            return gameServers;
        }

        var armaProcesses = gameServerHelpers.GetArmaProcessesWithCommandLine();
        await Task.WhenAll(gameServers.Select(server => UpdateServerStatus(server, armaProcesses)));
        return gameServers;
    }

    private async Task UpdateServerStatus(DomainGameServer gameServer, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        var matchingProcess = FindMatchingServerProcess(gameServer, armaProcesses);
        UpdateHeadlessClientProcessIds(gameServer, armaProcesses);

        if (matchingProcess is null)
        {
            gameServer.Status = new GameServerStatus();
            gameServer.ProcessId = null;
            StatusCache.TryRemove(gameServer.Id, out _);
            await gameServersContext.Replace(gameServer);
            return;
        }

        gameServer.ProcessId = matchingProcess.ProcessId;
        gameServer.Status.Launching = true;

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(5);
        try
        {
            var response = await client.GetAsync($"http://localhost:{gameServer.ApiPort}/server");
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                if (statusCode is 502 or 504)
                {
                    // Gateway error — server process exists but extension not reachable
                    gameServer.Status.Running = false;
                }
                else if (statusCode == 503)
                {
                    // Extension is up but has no status yet (startup)
                    // Leave current status unchanged, don't log
                }
                else
                {
                    // Unexpected error (e.g. 500) — log for visibility
                    var body = await response.Content.ReadAsStringAsync();
                    logger.LogWarning($"Status endpoint returned {statusCode} for '{gameServer.Name}': {body}");
                }
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                gameServer.Status = JsonSerializer.Deserialize<GameServerStatus>(content, DefaultJsonSerializerOptions.Options);
                gameServer.Status.ParsedUptime = gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(gameServer.Status.Uptime)).ToString();
                gameServer.Status.MaxPlayers = gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
                gameServer.Status.Running = true;
                gameServer.Status.Launching = false;
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

        // If we have recent event-based status cached, use it as it's more up-to-date than HTTP polling
        if (StatusCache.TryGetValue(gameServer.Id, out var cachedStatus) && cachedStatus.LastEventReceived > DateTime.UtcNow.AddSeconds(-30))
        {
            gameServer.Status = cachedStatus;
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

    public void WriteServerConfig(DomainGameServer gameServer, int playerCount, string missionSelection)
    {
        File.WriteAllText(
            gameServerHelpers.GetGameServerConfigPath(gameServer),
            gameServerHelpers.FormatGameServerConfig(gameServer, playerCount, missionSelection)
        );
    }

    public async Task LaunchGameServer(DomainGameServer gameServer, string missionName = null, string launchedBy = null)
    {
        gameServer.Status.Launching = true;

        if (missionName is not null)
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(missionName);
            var lastDot = nameWithoutExtension.LastIndexOf('.');
            if (lastDot > 0)
            {
                gameServer.Status.Mission = nameWithoutExtension[..lastDot];
                gameServer.Status.Map = nameWithoutExtension[(lastDot + 1)..];
            }
            else
            {
                gameServer.Status.Mission = nameWithoutExtension;
            }
        }

        if (launchedBy is not null)
        {
            gameServer.LaunchedBy = launchedBy;
        }

        var launchArguments = gameServerHelpers.FormatGameServerLaunchArguments(gameServer);
        gameServer.ProcessId = processUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments);

        await Task.Delay(TimeSpan.FromMilliseconds(50));

        for (var index = 0; index < gameServer.NumberHeadlessClients; index++)
        {
            launchArguments = gameServerHelpers.FormatHeadlessClientLaunchArguments(gameServer, index);
            gameServer.HeadlessClientProcessIds.Add(
                processUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments)
            );

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        await gameServersContext.Replace(gameServer);
    }

    public async Task StopGameServer(DomainGameServer gameServer)
    {
        if (!gameServer.Status.Running)
        {
            await KillGameServer(gameServer);
            return;
        }

        await SendShutdownAsync(gameServer.ApiPort, $"game server '{gameServer.Name}'");
    }

    private async Task SendShutdownAsync(int port, string context)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var content = new StringContent("{\"type\":\"shutdown\"}", System.Text.Encoding.UTF8, "application/json");
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

    public async Task KillGameServer(DomainGameServer gameServer)
    {
        if (gameServer.ProcessId is not null)
        {
            var process = processUtilities.FindProcessById(gameServer.ProcessId.Value);
            if (process is { HasExited: false })
            {
                process.Kill(true);
            }
        }

        gameServer.ProcessId = null;
        gameServer.LaunchedBy = null;
        gameServer.Status = new GameServerStatus();
        StatusCache.TryRemove(gameServer.Id, out _);

        gameServer.HeadlessClientProcessIds.ForEach(x =>
            {
                var hcProcess = processUtilities.FindProcessById(x);
                if (hcProcess is { HasExited: false })
                {
                    hcProcess.Kill(true);
                }
            }
        );
        gameServer.HeadlessClientProcessIds.Clear();

        await gameServersContext.Replace(gameServer);
    }

    public async Task<int> KillAllArmaProcesses()
    {
        var processes = gameServerHelpers.GetArmaProcesses().ToList();
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

        var gameServers = gameServersContext.Get().ToList();
        foreach (var gameServer in gameServers)
        {
            gameServer.ProcessId = null;
            gameServer.LaunchedBy = null;
            gameServer.Status = new GameServerStatus();
            gameServer.HeadlessClientProcessIds.Clear();
            StatusCache.TryRemove(gameServer.Id, out _);
            await gameServersContext.Replace(gameServer);
        }

        return processes.Count;
    }

    public List<GameServerMod> GetAvailableMods(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        Uri serverExecutable = new(gameServerHelpers.GetGameServerExecutablePath(gameServer));

        IEnumerable<string> availableModsFolders = [gameServerHelpers.GetGameServerModsPaths(gameServer.Environment)];
        availableModsFolders = availableModsFolders.Concat(gameServerHelpers.GetGameServerExtraModsPaths());

        var dlcModFoldersRegexString = gameServerHelpers.GetDlcModFoldersRegexString();
        Regex allowedPaths = new($"@.*|{dlcModFoldersRegexString}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex allowedExtensions = new("[ep]bo", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        List<GameServerMod> mods = [];
        foreach (var modsPath in availableModsFolders)
        {
            var modFolders = new DirectoryInfo(modsPath).EnumerateDirectories("*.*", SearchOption.AllDirectories).Where(x => allowedPaths.IsMatch(x.Name));
            foreach (var modFolder in modFolders)
            {
                if (mods.Any(x => x.Path == modFolder.FullName))
                {
                    continue;
                }

                var hasModFiles = new DirectoryInfo(modFolder.FullName).EnumerateFiles("*.*", SearchOption.AllDirectories)
                                                                       .Any(x => allowedExtensions.IsMatch(x.Extension));
                if (!hasModFiles)
                {
                    continue;
                }

                GameServerMod mod = new() { Name = modFolder.Name, Path = modFolder.FullName };
                Uri modFolderUri = new(mod.Path);
                if (serverExecutable.IsBaseOf(modFolderUri))
                {
                    mod.PathRelativeToServerExecutable = Uri.UnescapeDataString(serverExecutable.MakeRelativeUri(modFolderUri).ToString());
                }

                mods.Add(mod);
            }
        }

        foreach (var mod in mods)
        {
            if (mods.Any(x => x.Name == mod.Name && x.Path != mod.Path))
            {
                mod.IsDuplicate = true;
            }

            foreach (var duplicate in mods.Where(x => x.Name == mod.Name && x.Path != mod.Path))
            {
                duplicate.IsDuplicate = true;
            }
        }

        return mods;
    }

    public List<GameServerMod> GetEnvironmentMods(GameEnvironment environment)
    {
        var repoModsFolder = gameServerHelpers.GetGameServerModsPaths(environment);
        var modFolders = new DirectoryInfo(repoModsFolder).EnumerateDirectories("@*", SearchOption.TopDirectoryOnly);
        return modFolders
               .Select(modFolder => new { modFolder, modFiles = new DirectoryInfo(modFolder.FullName).EnumerateFiles("*.pbo", SearchOption.AllDirectories) })
               .Where(x => x.modFiles.Any())
               .Select(x => new GameServerMod { Name = x.modFolder.Name, Path = x.modFolder.FullName })
               .ToList();
    }

    public async Task HandleGameServerEvent(GameServerEvent gameServerEvent)
    {
        try
        {
            if (gameServerEvent.Type is not "server_status")
            {
                logger.LogInfo($"Game server event received: {gameServerEvent.Type} (apiPort {gameServerEvent.ApiPort})");
            }

            switch (gameServerEvent.Type)
            {
                case "server_status":       await HandleServerStatusEvent(gameServerEvent.ApiPort, gameServerEvent.Data); break;
                case "mission_stats":       await HandleMissionStatsEvent(gameServerEvent.Data); break;
                case "mission_started":     await HandleMissionLifecycleEvent(gameServerEvent.Data, isStart: true); break;
                case "mission_ended":       await HandleMissionLifecycleEvent(gameServerEvent.Data, isStart: false); break;
                case "player_connected":    await HandlePlayerPresenceEvent(gameServerEvent.Data, isConnected: true); break;
                case "player_disconnected": await HandlePlayerPresenceEvent(gameServerEvent.Data, isConnected: false); break;
                case "persistence_save":    await HandlePersistenceSaveEvent(gameServerEvent.Data); break;
                case "shutdown_complete":   await HandleShutdownCompleteEvent(gameServerEvent.ApiPort); break;
                default:                    logger.LogWarning($"Unknown game server event type: {gameServerEvent.Type}"); break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error handling game server event: {gameServerEvent.Type}", ex);
        }
    }

    private async Task HandleShutdownCompleteEvent(int apiPort)
    {
        var gameServer = gameServersContext.GetSingle(x => x.ApiPort == apiPort);
        if (gameServer is null)
        {
            logger.LogWarning($"Received shutdown_complete but no server matches apiPort {apiPort}");
            return;
        }

        foreach (var processId in gameServer.HeadlessClientProcessIds)
        {
            var process = processUtilities.FindProcessById(processId);
            if (process is { HasExited: false })
            {
                process.Kill(true);
            }
        }

        gameServer.HeadlessClientProcessIds.Clear();
        gameServer.Status = new GameServerStatus();
        StatusCache.TryRemove(gameServer.Id, out _);
        await gameServersContext.Replace(gameServer);

        await serversHub.Clients.All.ReceiveServerUpdate(new GameServerUpdate { Server = gameServer, InstanceCount = GetGameInstanceCount() });

        logger.LogInfo($"Server shutdown complete: {gameServer.Name} (apiPort {gameServer.ApiPort})");
    }

    private async Task HandleServerStatusEvent(int apiPort, Dictionary<string, object> data)
    {
        var gameServer = gameServersContext.GetSingle(x => x.ApiPort == apiPort);
        if (gameServer is null)
        {
            logger.LogWarning($"Received server_status but no server matches apiPort {apiPort}");
            return;
        }

        var status = StatusCache.GetOrAdd(gameServer.Id, _ => gameServer.Status ?? new GameServerStatus());

        if (data.TryGetValue("map", out var map)) status.Map = map.ToString();
        if (data.TryGetValue("mission", out var mission)) status.Mission = mission.ToString();
        if (data.TryGetValue("players", out var players) && players is JsonElement playersElement && playersElement.ValueKind == JsonValueKind.Array)
            status.Players = playersElement.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).ToList();
        if (data.TryGetValue("uptime", out var uptime) &&
            float.TryParse(uptime.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var uptimeValue))
        {
            status.Uptime = uptimeValue;
            status.ParsedUptime = gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(uptimeValue)).ToString();
            status.StartedAt ??= DateTime.UtcNow.AddSeconds(-uptimeValue);
        }

        if (data.TryGetValue("fps", out var fps) && float.TryParse(fps.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var fpsValue))
            status.Fps = fpsValue;
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

        await serversHub.Clients.All.ReceiveServerUpdate(new GameServerUpdate { Server = gameServer, InstanceCount = GetGameInstanceCount() });
    }

    private async Task HandleMissionStatsEvent(Dictionary<string, object> data)
    {
        var sessionId = data.TryGetValue("sessionId", out var sessionIdValue) ? sessionIdValue.ToString() : string.Empty;
        var mission = data.TryGetValue("mission", out var missionValue) ? missionValue.ToString() : string.Empty;
        var map = data.TryGetValue("map", out var mapValue) ? mapValue.ToString() : string.Empty;

        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(mission) || string.IsNullOrEmpty(map))
        {
            logger.LogWarning("mission_stats event missing sessionId, mission, or map");
            return;
        }

        var events = new List<string>();
        if (data.TryGetValue("events", out var eventsObj) && eventsObj is JsonElement jsonElement)
        {
            events.EnsureCapacity(jsonElement.GetArrayLength());
            foreach (var element in jsonElement.EnumerateArray())
            {
                events.Add(element.GetRawText());
            }
        }

        await publishEndpoint.Publish(
            new ProcessMissionStatsBatch
            {
                SessionId = sessionId,
                Mission = mission,
                Map = map,
                Events = events,
                ReceivedAt = DateTime.UtcNow
            }
        );
    }

    private async Task HandleMissionLifecycleEvent(Dictionary<string, object> data, bool isStart)
    {
        var sessionId = data.TryGetValue("sessionId", out var sessionIdValue) ? sessionIdValue.ToString() : string.Empty;
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var now = DateTime.UtcNow;

        if (isStart)
        {
            var mission = data.TryGetValue("mission", out var missionValue) ? missionValue.ToString() : string.Empty;
            var map = data.TryGetValue("map", out var mapValue) ? mapValue.ToString() : string.Empty;
            if (string.IsNullOrEmpty(mission) || string.IsNullOrEmpty(map))
            {
                return;
            }

            await missionStatsService.HandleMissionStartedAsync(sessionId, mission, map, now);
        }
        else
        {
            var duration = data.TryGetValue("duration", out var durationValue) && double.TryParse(durationValue.ToString(), out var durationSeconds)
                ? durationSeconds
                : 0;
            await missionStatsService.HandleMissionEndedAsync(sessionId, duration, now);
        }
    }

    private async Task HandlePlayerPresenceEvent(Dictionary<string, object> data, bool isConnected)
    {
        var sessionId = data.TryGetValue("sessionId", out var sessionIdValue) ? sessionIdValue.ToString() : string.Empty;
        var uid = data.TryGetValue("uid", out var uidValue) ? uidValue.ToString() : string.Empty;

        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(uid))
        {
            return;
        }

        var now = DateTime.UtcNow;

        if (isConnected)
        {
            var name = data.TryGetValue("name", out var nameValue) ? nameValue.ToString() : string.Empty;
            await missionStatsService.HandlePlayerConnectedAsync(sessionId, uid, name, now);
        }
        else
        {
            await missionStatsService.HandlePlayerDisconnectedAsync(sessionId, uid, now);
        }
    }

    private async Task HandlePersistenceSaveEvent(Dictionary<string, object> data)
    {
        var chunk = new ChunkEnvelope
        {
            Id = data.GetValueOrDefault("id")?.ToString() ?? string.Empty,
            Key = data.GetValueOrDefault("key")?.ToString() ?? string.Empty,
            Index = Convert.ToInt32(data.GetValueOrDefault("index", 0)),
            Total = Convert.ToInt32(data.GetValueOrDefault("total", 1)),
            Data = data.GetValueOrDefault("data")?.ToString() ?? string.Empty
        };

        await persistenceSessionsService.HandleSaveChunkAsync(chunk);
    }

    public void ClearStatusCache(string serverId)
    {
        StatusCache.TryRemove(serverId, out _);
    }

    public async Task UpdateGameServerOrder(OrderUpdateRequest orderUpdate)
    {
        foreach (var server in gameServersContext.Get())
        {
            if (server.Order == orderUpdate.PreviousIndex)
            {
                await gameServersContext.Update(server.Id, x => x.Order, orderUpdate.NewIndex);
            }
            else if (server.Order > orderUpdate.PreviousIndex && server.Order <= orderUpdate.NewIndex)
            {
                await gameServersContext.Update(server.Id, x => x.Order, server.Order - 1);
            }
            else if (server.Order < orderUpdate.PreviousIndex && server.Order >= orderUpdate.NewIndex)
            {
                await gameServersContext.Update(server.Id, x => x.Order, server.Order + 1);
            }
        }
    }
}
