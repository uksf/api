using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.ArmaServer.Services {
    public interface IGameServersService {
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
    }

    public class GameServersService : IGameServersService {
        private readonly IGameServerHelpers _gameServerHelpers;
        private readonly IGameServersContext _gameServersContext;
        private readonly IMissionPatchingService _missionPatchingService;

        public GameServersService(IGameServersContext gameServersContext, IMissionPatchingService missionPatchingService, IGameServerHelpers gameServerHelpers) {
            _gameServersContext = gameServersContext;
            _missionPatchingService = missionPatchingService;
            _gameServerHelpers = gameServerHelpers;
        }

        public int GetGameInstanceCount() {
            return _gameServerHelpers.GetArmaProcesses().Count();
        }

        public async Task UploadMissionFile(IFormFile file) {
            string fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
            string filePath = Path.Combine(_gameServerHelpers.GetGameServerMissionsPath(), fileName);
            await using FileStream stream = new(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
        }

        public List<MissionFile> GetMissionFiles() {
            IEnumerable<FileInfo> files = new DirectoryInfo(_gameServerHelpers.GetGameServerMissionsPath()).EnumerateFiles("*.pbo", SearchOption.TopDirectoryOnly);
            return files.Select(fileInfo => new MissionFile(fileInfo)).OrderBy(x => x.Map).ThenBy(x => x.Name).ToList();
        }

        public async Task GetGameServerStatus(GameServer gameServer) {
            if (gameServer.ProcessId != 0) {
                gameServer.Status.Started = Process.GetProcesses().Any(x => x.Id == gameServer.ProcessId);
                if (!gameServer.Status.Started) {
                    gameServer.ProcessId = 0;
                }
            }

            using HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            try {
                HttpResponseMessage response = await client.GetAsync($"http://localhost:{gameServer.ApiPort}/server");
                if (!response.IsSuccessStatusCode) {
                    gameServer.Status.Running = false;
                }

                string content = await response.Content.ReadAsStringAsync();
                gameServer.Status = JsonConvert.DeserializeObject<GameServerStatus>(content);
                gameServer.Status.ParsedUptime = _gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(gameServer.Status.Uptime)).ToString();
                gameServer.Status.MaxPlayers = _gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
                gameServer.Status.Running = true;
                gameServer.Status.Started = false;
            } catch (Exception) {
                gameServer.Status.Running = false;
            }
        }

        public async Task<List<GameServer>> GetAllGameServerStatuses() {
            List<GameServer> gameServers = _gameServersContext.Get().ToList();
            await Task.WhenAll(gameServers.Select(GetGameServerStatus));
            return gameServers;
        }

        public async Task<MissionPatchingResult> PatchMissionFile(string missionName) {
            // if (Data.GetSingle(x => x.status.mission == missionName) != null) { // TODO: Needs better server <-> api interaction to properly get running missions
            //     return new MissionPatchingResult {
            //         success = true,
            //         reports = new List<MissionPatchingReport> { new MissionPatchingReport("Mission in use", $"'{missionName}' is currently in use by another server.\nIt has not been patched.") }
            //     };
            // }

            string missionPath = Path.Combine(_gameServerHelpers.GetGameServerMissionsPath(), missionName);
            MissionPatchingResult result = await _missionPatchingService.PatchMission(
                missionPath,
                _gameServerHelpers.GetGameServerModsPaths(GameEnvironment.RELEASE),
                _gameServerHelpers.GetMaxCuratorCountFromSettings()
            );
            return result;
        }

        public void WriteServerConfig(GameServer gameServer, int playerCount, string missionSelection) {
            File.WriteAllText(_gameServerHelpers.GetGameServerConfigPath(gameServer), _gameServerHelpers.FormatGameServerConfig(gameServer, playerCount, missionSelection));
        }

        public async Task LaunchGameServer(GameServer gameServer) {
            string launchArguments = _gameServerHelpers.FormatGameServerLaunchArguments(gameServer);
            gameServer.ProcessId = ProcessUtilities.LaunchManagedProcess(_gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments);

            await Task.Delay(TimeSpan.FromSeconds(1));

            // launch headless clients
            if (gameServer.NumberHeadlessClients > 0) {
                for (int index = 0; index < gameServer.NumberHeadlessClients; index++) {
                    launchArguments = _gameServerHelpers.FormatHeadlessClientLaunchArguments(gameServer, index);
                    gameServer.HeadlessClientProcessIds.Add(ProcessUtilities.LaunchManagedProcess(_gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments));

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }

        public async Task StopGameServer(GameServer gameServer) {
            try {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                await client.GetAsync($"http://localhost:{gameServer.ApiPort}/server/stop");
            } catch (Exception) {
                // ignored
            }

            if (gameServer.NumberHeadlessClients > 0) {
                for (int index = 0; index < gameServer.NumberHeadlessClients; index++) {
                    try {
                        using HttpClient client = new();
                        client.DefaultRequestHeaders.Accept.Add(new("application/json"));
                        await client.GetAsync($"http://localhost:{gameServer.ApiPort + index + 1}/server/stop");
                    } catch (Exception) {
                        // ignored
                    }
                }
            }
        }

        public void KillGameServer(GameServer gameServer) {
            if (!gameServer.ProcessId.HasValue) {
                throw new NullReferenceException();
            }

            Process process = Process.GetProcesses().FirstOrDefault(x => x.Id == gameServer.ProcessId.Value);
            if (process != null && !process.HasExited) {
                process.Kill();
            }

            gameServer.ProcessId = null;

            gameServer.HeadlessClientProcessIds.ForEach(
                x => {
                    process = Process.GetProcesses().FirstOrDefault(y => y.Id == x);
                    if (process != null && !process.HasExited) {
                        process.Kill();
                    }
                }
            );
            gameServer.HeadlessClientProcessIds.Clear();
        }

        public int KillAllArmaProcesses() {
            List<Process> processes = _gameServerHelpers.GetArmaProcesses().ToList();
            foreach (Process process in processes) {
                process.Kill();
            }

            _gameServersContext.Get()
                               .ToList()
                               .ForEach(
                                   x => {
                                       x.ProcessId = null;
                                       x.HeadlessClientProcessIds.Clear();
                                   }
                               );
            return processes.Count;
        }

        public List<GameServerMod> GetAvailableMods(string id) {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            Uri serverExecutable = new(_gameServerHelpers.GetGameServerExecutablePath(gameServer));

            IEnumerable<string> availableModsFolders = new[] { _gameServerHelpers.GetGameServerModsPaths(gameServer.Environment) };
            availableModsFolders = availableModsFolders.Concat(_gameServerHelpers.GetGameServerExtraModsPaths());

            List<GameServerMod> mods = new();
            foreach (string modsPath in availableModsFolders) {
                IEnumerable<DirectoryInfo> modFolders = GetModFolders(modsPath);
                foreach (DirectoryInfo modFolder in modFolders) {
                    if (mods.Any(x => x.Path == modFolder.FullName)) continue;

                    IEnumerable<FileInfo> modFiles = new DirectoryInfo(modFolder.FullName).EnumerateFiles("*.pbo", SearchOption.AllDirectories);
                    if (!modFiles.Any()) continue;

                    GameServerMod mod = new() { Name = modFolder.Name, Path = modFolder.FullName };
                    Uri modFolderUri = new(mod.Path);
                    if (serverExecutable.IsBaseOf(modFolderUri)) {
                        mod.PathRelativeToServerExecutable = Uri.UnescapeDataString(serverExecutable.MakeRelativeUri(modFolderUri).ToString());
                    }

                    mods.Add(mod);
                }
            }

            foreach (GameServerMod mod in mods) {
                if (mods.Any(x => x.Name == mod.Name && x.Path != mod.Path)) {
                    mod.IsDuplicate = true;
                }

                foreach (GameServerMod duplicate in mods.Where(x => x.Name == mod.Name && x.Path != mod.Path)) {
                    duplicate.IsDuplicate = true;
                }
            }

            return mods;
        }

        public List<GameServerMod> GetEnvironmentMods(GameEnvironment environment) {
            string repoModsFolder = _gameServerHelpers.GetGameServerModsPaths(environment);
            IEnumerable<DirectoryInfo> modFolders = new DirectoryInfo(repoModsFolder).EnumerateDirectories("@*", SearchOption.TopDirectoryOnly);
            return modFolders.Select(modFolder => new { modFolder, modFiles = new DirectoryInfo(modFolder.FullName).EnumerateFiles("*.pbo", SearchOption.AllDirectories) })
                             .Where(x => x.modFiles.Any())
                             .Select(x => new GameServerMod { Name = x.modFolder.Name, Path = x.modFolder.FullName })
                             .ToList();
        }

        private static IEnumerable<DirectoryInfo> GetModFolders(string modsPath) {
            List<DirectoryInfo> modFolders = new DirectoryInfo(modsPath).EnumerateDirectories("@*", SearchOption.TopDirectoryOnly).ToList();

            DirectoryInfo vnPath = new(Path.Join(modsPath, "vn"));
            if (vnPath.Exists) {
                modFolders.Add(vnPath);
            }

            DirectoryInfo gmPath = new(Path.Join(modsPath, "gm"));
            if (gmPath.Exists) {
                modFolders.Add(gmPath);
            }

            return modFolders;
        }
    }
}
