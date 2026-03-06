using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
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
    Task UploadMissionFile(IFormFile file);
    List<MissionFile> GetMissionFiles();
    Task GetGameServerStatus(DomainGameServer gameServer);
    Task<List<DomainGameServer>> GetAllGameServerStatuses();
    Task<MissionPatchingResult> PatchMissionFile(string missionName);
    void WriteServerConfig(DomainGameServer gameServer, int playerCount, string missionSelection);
    Task LaunchGameServer(DomainGameServer gameServer, string missionName = null, string launchedBy = null);
    Task StopGameServer(DomainGameServer gameServer);
    Task KillGameServer(DomainGameServer gameServer);
    Task<int> KillAllArmaProcesses();
    List<GameServerMod> GetAvailableMods(string id);
    List<GameServerMod> GetEnvironmentMods(GameEnvironment environment);
    Task UpdateGameServerOrder(OrderUpdateRequest orderUpdate);
    Task HandleGameServerEvent(GameServerEvent gameServerEvent);
}

public class GameServersService(
    IGameServersContext gameServersContext,
    IMissionPatchingService missionPatchingService,
    IGameServerHelpers gameServerHelpers,
    IProcessUtilities processUtilities,
    IHttpClientFactory httpClientFactory,
    IVariablesService variablesService,
    IHubContext<ServersHub, IServersClient> serversHub,
    IUksfLogger logger,
    IPublishEndpoint publishEndpoint,
    IPersistenceSessionsService persistenceSessionsService
) : IGameServersService
{
    private static readonly ConcurrentDictionary<string, GameServerStatus> StatusCache = new();

    public int GetGameInstanceCount()
    {
        return gameServerHelpers.GetArmaProcesses().Count();
    }

    public async Task UploadMissionFile(IFormFile file)
    {
        var fileName = Path.GetFileName(ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"'));
        var filePath = Path.Combine(gameServerHelpers.GetGameServerMissionsPath(), fileName);
        await using FileStream stream = new(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
    }

    public List<MissionFile> GetMissionFiles()
    {
        var files = new DirectoryInfo(gameServerHelpers.GetGameServerMissionsPath()).EnumerateFiles("*.pbo", SearchOption.TopDirectoryOnly);
        return files.Select(fileInfo => new MissionFile(fileInfo)).OrderBy(x => x.Map).ThenBy(x => x.Name).ToList();
    }

    public async Task GetGameServerStatus(DomainGameServer gameServer)
    {
        if (variablesService.GetFeatureState("SKIP_SERVER_STATUS"))
        {
            await gameServersContext.Replace(gameServer);
            return;
        }

        if (!gameServerHelpers.GetArmaProcesses().Any())
        {
            gameServer.Status = new GameServerStatus();
            gameServer.ProcessId = null;
            StatusCache.TryRemove(gameServer.Id, out _);
            await gameServersContext.Replace(gameServer);
            return;
        }

        var armaProcesses = gameServerHelpers.GetArmaProcessesWithCommandLine();
        await UpdateServerStatus(gameServer, armaProcesses);
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

        var armaProcesses = gameServerHelpers.GetArmaProcessesWithCommandLine();
        if (armaProcesses.Count == 0)
        {
            StatusCache.Clear();
            foreach (var gameServer in gameServers)
            {
                gameServer.Status = new GameServerStatus();
                gameServer.ProcessId = null;
                await gameServersContext.Replace(gameServer);
            }

            return gameServers;
        }

        await Task.WhenAll(gameServers.Select(server => UpdateServerStatus(server, armaProcesses)));
        return gameServers;
    }

    private async Task UpdateServerStatus(DomainGameServer gameServer, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        var matchingProcess = FindMatchingProcess(gameServer, armaProcesses);
        if (matchingProcess is null)
        {
            gameServer.Status.Running = false;
            gameServer.Status.Started = false;
            gameServer.ProcessId = null;
            StatusCache.TryRemove(gameServer.Id, out _);
            await gameServersContext.Replace(gameServer);
            return;
        }

        gameServer.ProcessId = matchingProcess.ProcessId;
        gameServer.Status.Started = true;

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(5);
        try
        {
            var response = await client.GetAsync($"http://localhost:{gameServer.ApiPort}/server");
            if (!response.IsSuccessStatusCode)
            {
                gameServer.Status.Running = false;
            }

            var content = await response.Content.ReadAsStringAsync();
            gameServer.Status = JsonSerializer.Deserialize<GameServerStatus>(content, DefaultJsonSerializerOptions.Options);
            gameServer.Status.ParsedUptime = gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(gameServer.Status.Uptime)).ToString();
            gameServer.Status.MaxPlayers = gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
            gameServer.Status.Running = true;
            gameServer.Status.Started = false;
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

    private static ProcessCommandLineInfo FindMatchingProcess(DomainGameServer gameServer, IReadOnlyList<ProcessCommandLineInfo> armaProcesses)
    {
        var portArg = $"-port={gameServer.Port} ";

        // Prefer the main server process (has -config=) over headless clients (have -client)
        var mainProcess = armaProcesses.FirstOrDefault(p => p.CommandLine.Contains(portArg) && p.CommandLine.Contains("-config="));
        if (mainProcess is not null)
        {
            return mainProcess;
        }

        // Fall back to any process matching the port (including headless clients)
        return armaProcesses.FirstOrDefault(p => p.CommandLine.Contains(portArg) || p.CommandLine.EndsWith($"-port={gameServer.Port}"));
    }

    public async Task<MissionPatchingResult> PatchMissionFile(string missionName)
    {
        var missionPath = Path.Combine(gameServerHelpers.GetGameServerMissionsPath(), Path.GetFileName(missionName));
        return await missionPatchingService.PatchMission(
            missionPath,
            gameServerHelpers.GetGameServerModsPaths(GameEnvironment.Release),
            gameServerHelpers.GetMaxCuratorCountFromSettings()
        );
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
        var launchArguments = gameServerHelpers.FormatGameServerLaunchArguments(gameServer);
        gameServer.ProcessId = processUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments);

        await Task.Delay(TimeSpan.FromSeconds(1));

        // launch headless clients
        if (gameServer.NumberHeadlessClients > 0)
        {
            for (var index = 0; index < gameServer.NumberHeadlessClients; index++)
            {
                launchArguments = gameServerHelpers.FormatHeadlessClientLaunchArguments(gameServer, index);
                gameServer.HeadlessClientProcessIds.Add(
                    processUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments)
                );

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        if (missionName is not null)
        {
            gameServer.Status.Mission = missionName;
        }

        if (launchedBy is not null)
        {
            gameServer.LaunchedBy = launchedBy;
        }

        await gameServersContext.Replace(gameServer);
    }

    public async Task StopGameServer(DomainGameServer gameServer)
    {
        await SendShutdownAsync(gameServer.ApiPort, $"game server '{gameServer.Name}'");

        for (var index = 0; index < gameServer.NumberHeadlessClients; index++)
        {
            await SendShutdownAsync(gameServer.ApiPort + index + 1, $"headless client {index} for '{gameServer.Name}'");
        }
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
        if (gameServer.ProcessId is null)
        {
            throw new InvalidOperationException("Process ID not found");
        }

        var process = processUtilities.FindProcessById(gameServer.ProcessId.Value);
        if (process is { HasExited: false })
        {
            process.Kill(true);
        }

        gameServer.ProcessId = null;
        gameServer.LaunchedBy = null;
        StatusCache.TryRemove(gameServer.Id, out _);

        gameServer.HeadlessClientProcessIds.ForEach(x =>
            {
                process = processUtilities.FindProcessById(x);
                if (process is { HasExited: false })
                {
                    process.Kill(true);
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

        var gameServers = gameServersContext.Get().ToList();
        foreach (var gameServer in gameServers)
        {
            gameServer.ProcessId = null;
            gameServer.LaunchedBy = null;
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
            switch (gameServerEvent.Type)
            {
                case "server_status":    HandleServerStatusEvent(gameServerEvent.Data); break;
                case "performance":      HandlePerformanceEvent(gameServerEvent.Data); break;
                case "mission_stats":    await HandleMissionStatsEvent(gameServerEvent.Data); break;
                case "persistence_save": await HandlePersistenceSaveEvent(gameServerEvent.Data); break;
                case "player_connected":
                case "player_disconnected":
                case "mission_started":
                case "mission_ended": logger.LogInfo($"Game server event: {gameServerEvent.Type}"); break;
                default: logger.LogWarning($"Unknown game server event type: {gameServerEvent.Type}"); break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error handling game server event: {gameServerEvent.Type}", ex);
        }

        // Always notify web clients, even if event handling failed
        await serversHub.Clients.All.ReceiveAnyUpdateIfNotCaller(string.Empty, false);
    }

    // TODO: Events don't currently identify which server sent them. When the Rust extension
    // exposes apiPort back to SQF, add an "apiPort" field to event data and match to a specific server.
    private void HandleServerStatusEvent(Dictionary<string, object> data)
    {
        ApplyToRunningServerCaches((gameServer, status) =>
            {
                if (data.TryGetValue("map", out var map)) status.Map = map.ToString();
                if (data.TryGetValue("mission", out var mission)) status.Mission = mission.ToString();
                if (data.TryGetValue("players", out var players) && int.TryParse(players.ToString(), out var playerCount)) status.Players = playerCount;
                if (data.TryGetValue("uptime", out var uptime) && float.TryParse(uptime.ToString(), out var uptimeValue))
                {
                    status.Uptime = uptimeValue;
                    status.ParsedUptime = gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(uptimeValue)).ToString();
                }

                status.Running = true;
                status.Started = false;
                status.MaxPlayers = gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
            }
        );
    }

    private void HandlePerformanceEvent(Dictionary<string, object> data)
    {
        ApplyToRunningServerCaches((_, status) =>
            {
                if (data.TryGetValue("fps", out var fps) && float.TryParse(fps.ToString(), out var fpsValue)) status.Fps = fpsValue;
                if (data.TryGetValue("entityCount", out var entityCount) && int.TryParse(entityCount.ToString(), out var entityCountValue))
                    status.EntityCount = entityCountValue;
                if (data.TryGetValue("aiCount", out var aiCount) && int.TryParse(aiCount.ToString(), out var aiCountValue)) status.AiCount = aiCountValue;
                if (data.TryGetValue("headlessClientCount", out var headlessClientCount) &&
                    int.TryParse(headlessClientCount.ToString(), out var headlessClientCountValue)) status.HeadlessClientCount = headlessClientCountValue;
            }
        );
    }

    private void ApplyToRunningServerCaches(Action<DomainGameServer, GameServerStatus> applyUpdate)
    {
        foreach (var gameServer in GetRunningGameServers())
        {
            try
            {
                var status = StatusCache.GetOrAdd(gameServer.Id, _ => new GameServerStatus());
                applyUpdate(gameServer, status);
                status.LastEventReceived = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error updating status cache for server '{gameServer.Name}'", ex);
            }
        }
    }

    private async Task HandleMissionStatsEvent(Dictionary<string, object> data)
    {
        var mission = data.TryGetValue("mission", out var missionValue) ? missionValue.ToString() : string.Empty;
        var map = data.TryGetValue("map", out var mapValue) ? mapValue.ToString() : string.Empty;

        if (string.IsNullOrEmpty(mission) || string.IsNullOrEmpty(map))
        {
            logger.LogWarning("mission_stats event missing mission or map");
            return;
        }

        var events = new List<BsonDocument>();
        if (data.TryGetValue("events", out var eventsObj) && eventsObj is JsonElement jsonElement)
        {
            events.EnsureCapacity(jsonElement.GetArrayLength());
            foreach (var element in jsonElement.EnumerateArray())
            {
                events.Add(BsonDocument.Parse(element.GetRawText()));
            }
        }

        await publishEndpoint.Publish(
            new ProcessMissionStatsBatch
            {
                Mission = mission,
                Map = map,
                Events = events,
                ReceivedAt = DateTime.UtcNow
            }
        );

        logger.LogInfo($"Published mission_stats batch: {mission} on {map}, {events.Count} events");
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

    private List<DomainGameServer> GetRunningGameServers()
    {
        return gameServersContext.Get().Where(server => server.ProcessId is not null).ToList();
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
