using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
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
    Task LaunchGameServer(DomainGameServer gameServer);
    Task StopGameServer(DomainGameServer gameServer);
    Task KillGameServer(DomainGameServer gameServer);
    Task<int> KillAllArmaProcesses();
    List<GameServerMod> GetAvailableMods(string id);
    List<GameServerMod> GetEnvironmentMods(GameEnvironment environment);
    Task UpdateGameServerOrder(OrderUpdateRequest orderUpdate);
}

public class GameServersService(
    IGameServersContext gameServersContext,
    IMissionPatchingService missionPatchingService,
    IGameServerHelpers gameServerHelpers,
    IVariablesService variablesService,
    IUksfLogger logger
) : IGameServersService
{
    public int GetGameInstanceCount()
    {
        return gameServerHelpers.GetArmaProcesses().Count();
    }

    public async Task UploadMissionFile(IFormFile file)
    {
        var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
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

        if (gameServer.ProcessId != null && gameServer.ProcessId != 0)
        {
            gameServer.Status.Started = Process.GetProcesses().Any(x => x.Id == gameServer.ProcessId);
            if (!gameServer.Status.Started)
            {
                gameServer.ProcessId = null;
            }
        }
        else
        {
            gameServer.Status.Started = false;
        }

        using HttpClient client = new();
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
        finally
        {
            await gameServersContext.Replace(gameServer);
        }
    }

    public async Task<List<DomainGameServer>> GetAllGameServerStatuses()
    {
        var gameServers = gameServersContext.Get().ToList();
        await Task.WhenAll(gameServers.Select(GetGameServerStatus));
        return gameServers;
    }

    public async Task<MissionPatchingResult> PatchMissionFile(string missionName)
    {
        // if (Data.GetSingle(x => x.status.mission == missionName) != null) { // TODO: Needs better server <-> api interaction to properly get running missions
        //     return new MissionPatchingResult {
        //         success = true,
        //         reports = new List<MissionPatchingReport> { new MissionPatchingReport("Mission in use", $"'{missionName}' is currently in use by another server.\nIt has not been patched.") }
        //     };
        // }

        var missionPath = Path.Combine(gameServerHelpers.GetGameServerMissionsPath(), missionName);
        var result = await missionPatchingService.PatchMission(
            missionPath,
            gameServerHelpers.GetGameServerModsPaths(GameEnvironment.Release),
            gameServerHelpers.GetMaxCuratorCountFromSettings()
        );
        return result;
    }

    public void WriteServerConfig(DomainGameServer gameServer, int playerCount, string missionSelection)
    {
        File.WriteAllText(
            gameServerHelpers.GetGameServerConfigPath(gameServer),
            gameServerHelpers.FormatGameServerConfig(gameServer, playerCount, missionSelection)
        );
    }

    public async Task LaunchGameServer(DomainGameServer gameServer)
    {
        var launchArguments = gameServerHelpers.FormatGameServerLaunchArguments(gameServer);
        gameServer.ProcessId = ProcessUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments);

        await Task.Delay(TimeSpan.FromSeconds(1));

        // launch headless clients
        if (gameServer.NumberHeadlessClients > 0)
        {
            for (var index = 0; index < gameServer.NumberHeadlessClients; index++)
            {
                launchArguments = gameServerHelpers.FormatHeadlessClientLaunchArguments(gameServer, index);
                gameServer.HeadlessClientProcessIds.Add(
                    ProcessUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments)
                );

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        await gameServersContext.Replace(gameServer);
    }

    public async Task StopGameServer(DomainGameServer gameServer)
    {
        try
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            await client.GetAsync($"http://localhost:{gameServer.ApiPort}/server/stop");
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        catch (Exception exception)
        {
            logger.LogError($"Unexpected error stopping game server '{gameServer.Name}'", exception);
        }

        if (gameServer.NumberHeadlessClients > 0)
        {
            for (var index = 0; index < gameServer.NumberHeadlessClients; index++)
            {
                try
                {
                    using HttpClient client = new();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    await client.GetAsync($"http://localhost:{gameServer.ApiPort + index + 1}/server/stop");
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                catch (Exception exception)
                {
                    logger.LogError($"Unexpected error stopping headless client {index} for '{gameServer.Name}'", exception);
                }
            }
        }
    }

    public async Task KillGameServer(DomainGameServer gameServer)
    {
        if (gameServer.ProcessId is null)
        {
            throw new NullReferenceException("Process ID not found");
        }

        var process = Process.GetProcesses().FirstOrDefault(x => x.Id == gameServer.ProcessId.Value);
        if (process is { HasExited: false })
        {
            process.Kill(true);
        }

        gameServer.ProcessId = null;

        gameServer.HeadlessClientProcessIds.ForEach(x =>
            {
                process = Process.GetProcesses().FirstOrDefault(y => y.Id == x);
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
            gameServer.HeadlessClientProcessIds.Clear();
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
