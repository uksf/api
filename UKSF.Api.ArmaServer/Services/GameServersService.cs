using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Request;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServersService
{
    int GetGameInstanceCount();
    Task UploadMissionFile(IFormFile file);
    List<MissionFile> GetMissionFiles();
    Task GetGameServerStatus(GameServer gameServer);
    Task<List<GameServer>> GetAllGameServerStatuses();
    Task<MissionPatchingResult> PatchMissionFile(string missionName);
    void WriteServerConfig(GameServer gameServer, int playerCount, string missionSelection);
    Task LaunchGameServer(GameServer gameServer);
    Task StopGameServer(GameServer gameServer);
    void KillGameServer(GameServer gameServer);
    int KillAllArmaProcesses();
    List<GameServerMod> GetAvailableMods(string id);
    List<GameServerMod> GetEnvironmentMods(GameEnvironment environment);
    Task UpdateGameServerOrder(OrderUpdateRequest orderUpdate);
}

public class GameServersService : IGameServersService
{
    private readonly IGameServerHelpers _gameServerHelpers;
    private readonly IVariablesService _variablesService;
    private readonly IGameServersContext _gameServersContext;
    private readonly IMissionPatchingService _missionPatchingService;

    public GameServersService(
        IGameServersContext gameServersContext,
        IMissionPatchingService missionPatchingService,
        IGameServerHelpers gameServerHelpers,
        IVariablesService variablesService
    )
    {
        _gameServersContext = gameServersContext;
        _missionPatchingService = missionPatchingService;
        _gameServerHelpers = gameServerHelpers;
        _variablesService = variablesService;
    }

    public int GetGameInstanceCount()
    {
        return _gameServerHelpers.GetArmaProcesses().Count();
    }

    public async Task UploadMissionFile(IFormFile file)
    {
        var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
        var filePath = Path.Combine(_gameServerHelpers.GetGameServerMissionsPath(), fileName);
        await using FileStream stream = new(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
    }

    public List<MissionFile> GetMissionFiles()
    {
        var files = new DirectoryInfo(_gameServerHelpers.GetGameServerMissionsPath()).EnumerateFiles("*.pbo", SearchOption.TopDirectoryOnly);
        return files.Select(fileInfo => new MissionFile(fileInfo)).OrderBy(x => x.Map).ThenBy(x => x.Name).ToList();
    }

    public async Task GetGameServerStatus(GameServer gameServer)
    {
        if (_variablesService.GetFeatureState("SKIP_SERVER_STATUS"))
        {
            return;
        }

        if (gameServer.ProcessId != 0)
        {
            gameServer.Status.Started = Process.GetProcesses().Any(x => x.Id == gameServer.ProcessId);
            if (!gameServer.Status.Started)
            {
                gameServer.ProcessId = 0;
            }
        }

        using HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        try
        {
            var response = await client.GetAsync($"http://localhost:{gameServer.ApiPort}/server");
            if (!response.IsSuccessStatusCode)
            {
                gameServer.Status.Running = false;
            }

            var content = await response.Content.ReadAsStringAsync();
            gameServer.Status = JsonSerializer.Deserialize<GameServerStatus>(content, DefaultJsonSerializerOptions.Options);
            gameServer.Status.ParsedUptime = _gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(gameServer.Status.Uptime)).ToString();
            gameServer.Status.MaxPlayers = _gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
            gameServer.Status.Running = true;
            gameServer.Status.Started = false;
        }
        catch (Exception)
        {
            gameServer.Status.Running = false;
        }
    }

    public async Task<List<GameServer>> GetAllGameServerStatuses()
    {
        var gameServers = _gameServersContext.Get().ToList();
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

        var missionPath = Path.Combine(_gameServerHelpers.GetGameServerMissionsPath(), missionName);
        var result = await _missionPatchingService.PatchMission(
            missionPath,
            _gameServerHelpers.GetGameServerModsPaths(GameEnvironment.RELEASE),
            _gameServerHelpers.GetMaxCuratorCountFromSettings()
        );
        return result;
    }

    public void WriteServerConfig(GameServer gameServer, int playerCount, string missionSelection)
    {
        File.WriteAllText(
            _gameServerHelpers.GetGameServerConfigPath(gameServer),
            _gameServerHelpers.FormatGameServerConfig(gameServer, playerCount, missionSelection)
        );
    }

    public async Task LaunchGameServer(GameServer gameServer)
    {
        var launchArguments = _gameServerHelpers.FormatGameServerLaunchArguments(gameServer);
        gameServer.ProcessId = ProcessUtilities.LaunchManagedProcess(_gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments);

        await Task.Delay(TimeSpan.FromSeconds(1));

        // launch headless clients
        if (gameServer.NumberHeadlessClients > 0)
        {
            for (var index = 0; index < gameServer.NumberHeadlessClients; index++)
            {
                launchArguments = _gameServerHelpers.FormatHeadlessClientLaunchArguments(gameServer, index);
                gameServer.HeadlessClientProcessIds.Add(
                    ProcessUtilities.LaunchManagedProcess(_gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments)
                );

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }

    public async Task StopGameServer(GameServer gameServer)
    {
        try
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            await client.GetAsync($"http://localhost:{gameServer.ApiPort}/server/stop");
        }
        catch (Exception)
        {
            // ignored
        }

        if (gameServer.NumberHeadlessClients > 0)
        {
            for (var index = 0; index < gameServer.NumberHeadlessClients; index++)
            {
                try
                {
                    using HttpClient client = new();
                    client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                    await client.GetAsync($"http://localhost:{gameServer.ApiPort + index + 1}/server/stop");
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }

    public void KillGameServer(GameServer gameServer)
    {
        if (!gameServer.ProcessId.HasValue)
        {
            throw new NullReferenceException();
        }

        var process = Process.GetProcesses().FirstOrDefault(x => x.Id == gameServer.ProcessId.Value);
        if (process is { HasExited: false })
        {
            process.Kill();
        }

        gameServer.ProcessId = null;

        gameServer.HeadlessClientProcessIds.ForEach(
            x =>
            {
                process = Process.GetProcesses().FirstOrDefault(y => y.Id == x);
                if (process is { HasExited: false })
                {
                    process.Kill();
                }
            }
        );
        gameServer.HeadlessClientProcessIds.Clear();
    }

    public int KillAllArmaProcesses()
    {
        var processes = _gameServerHelpers.GetArmaProcesses().ToList();
        foreach (var process in processes)
        {
            process.Kill();
        }

        _gameServersContext.Get()
                           .ToList()
                           .ForEach(
                               x =>
                               {
                                   x.ProcessId = null;
                                   x.HeadlessClientProcessIds.Clear();
                               }
                           );
        return processes.Count;
    }

    public List<GameServerMod> GetAvailableMods(string id)
    {
        var gameServer = _gameServersContext.GetSingle(id);
        Uri serverExecutable = new(_gameServerHelpers.GetGameServerExecutablePath(gameServer));

        IEnumerable<string> availableModsFolders = new[] { _gameServerHelpers.GetGameServerModsPaths(gameServer.Environment) };
        availableModsFolders = availableModsFolders.Concat(_gameServerHelpers.GetGameServerExtraModsPaths());

        var dlcModFoldersRegexString = _gameServerHelpers.GetDlcModFoldersRegexString();
        Regex allowedPaths = new($"@.*|{dlcModFoldersRegexString}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex allowedExtensions = new("[ep]bo", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        List<GameServerMod> mods = new();
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
        var repoModsFolder = _gameServerHelpers.GetGameServerModsPaths(environment);
        var modFolders = new DirectoryInfo(repoModsFolder).EnumerateDirectories("@*", SearchOption.TopDirectoryOnly);
        return modFolders
               .Select(modFolder => new { modFolder, modFiles = new DirectoryInfo(modFolder.FullName).EnumerateFiles("*.pbo", SearchOption.AllDirectories) })
               .Where(x => x.modFiles.Any())
               .Select(x => new GameServerMod { Name = x.modFolder.Name, Path = x.modFolder.FullName })
               .ToList();
    }

    public async Task UpdateGameServerOrder(OrderUpdateRequest orderUpdate)
    {
        foreach (var server in _gameServersContext.Get())
        {
            if (server.Order == orderUpdate.PreviousIndex)
            {
                await _gameServersContext.Update(server.Id, x => x.Order, orderUpdate.NewIndex);
            }
            else if (server.Order > orderUpdate.PreviousIndex && server.Order <= orderUpdate.NewIndex)
            {
                await _gameServersContext.Update(server.Id, x => x.Order, server.Order - 1);
            }
            else if (server.Order < orderUpdate.PreviousIndex && server.Order >= orderUpdate.NewIndex)
            {
                await _gameServersContext.Update(server.Id, x => x.Order, server.Order + 1);
            }
        }
    }
}
