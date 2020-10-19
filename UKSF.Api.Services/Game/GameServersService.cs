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
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Game;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Mission;
using UKSF.Common;

namespace UKSF.Api.Services.Game {
    public class GameServersService : DataBackedService<IGameServersDataService>, IGameServersService {
        private readonly IGameServerHelpers gameServerHelpers;
        private readonly IMissionPatchingService missionPatchingService;

        public GameServersService(IGameServersDataService data, IMissionPatchingService missionPatchingService, IGameServerHelpers gameServerHelpers) : base(data) {
            this.missionPatchingService = missionPatchingService;
            this.gameServerHelpers = gameServerHelpers;
        }

        public int GetGameInstanceCount() => gameServerHelpers.GetArmaProcesses().Count();

        public async Task UploadMissionFile(IFormFile file) {
            string fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
            string filePath = Path.Combine(gameServerHelpers.GetGameServerMissionsPath(), fileName);
            await using FileStream stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
        }

        public List<MissionFile> GetMissionFiles() {
            IEnumerable<FileInfo> files = new DirectoryInfo(gameServerHelpers.GetGameServerMissionsPath()).EnumerateFiles("*.pbo", SearchOption.TopDirectoryOnly);
            return files.Select(fileInfo => new MissionFile(fileInfo)).OrderBy(x => x.map).ThenBy(x => x.name).ToList();
        }

        public async Task GetGameServerStatus(GameServer gameServer) {
            if (gameServer.processId != 0) {
                gameServer.status.started = Process.GetProcesses().Any(x => x.Id == gameServer.processId);
                if (!gameServer.status.started) {
                    gameServer.processId = 0;
                }
            }

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try {
                HttpResponseMessage response = await client.GetAsync($"http://localhost:{gameServer.apiPort}/server");
                if (!response.IsSuccessStatusCode) {
                    gameServer.status.running = false;
                }

                string content = await response.Content.ReadAsStringAsync();
                gameServer.status = JsonConvert.DeserializeObject<GameServerStatus>(content);
                gameServer.status.parsedUptime = gameServerHelpers.StripMilliseconds(TimeSpan.FromSeconds(gameServer.status.uptime)).ToString();
                gameServer.status.maxPlayers = gameServerHelpers.GetMaxPlayerCountFromConfig(gameServer);
                gameServer.status.running = true;
                gameServer.status.started = false;
            } catch (Exception) {
                gameServer.status.running = false;
            }
        }

        public async Task<List<GameServer>> GetAllGameServerStatuses() {
            List<GameServer> gameServers = Data.Get().ToList();
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

            string missionPath = Path.Combine(gameServerHelpers.GetGameServerMissionsPath(), missionName);
            MissionPatchingResult result = await missionPatchingService.PatchMission(missionPath);
            return result;
        }

        public void WriteServerConfig(GameServer gameServer, int playerCount, string missionSelection) =>
            File.WriteAllText(gameServerHelpers.GetGameServerConfigPath(gameServer), gameServerHelpers.FormatGameServerConfig(gameServer, playerCount, missionSelection));

        public async Task LaunchGameServer(GameServer gameServer) {
            string launchArguments = gameServerHelpers.FormatGameServerLaunchArguments(gameServer);
            gameServer.processId = ProcessUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments);

            await Task.Delay(TimeSpan.FromSeconds(1));

            // launch headless clients
            if (gameServer.numberHeadlessClients > 0) {
                for (int index = 0; index < gameServer.numberHeadlessClients; index++) {
                    launchArguments = gameServerHelpers.FormatHeadlessClientLaunchArguments(gameServer, index);
                    gameServer.headlessClientProcessIds.Add(ProcessUtilities.LaunchManagedProcess(gameServerHelpers.GetGameServerExecutablePath(gameServer), launchArguments));

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }

        public async Task StopGameServer(GameServer gameServer) {
            try {
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                await client.GetAsync($"http://localhost:{gameServer.apiPort}/server/stop");
            } catch (Exception) {
                // ignored
            }

            if (gameServer.numberHeadlessClients > 0) {
                for (int index = 0; index < gameServer.numberHeadlessClients; index++) {
                    try {
                        using HttpClient client = new HttpClient();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        await client.GetAsync($"http://localhost:{gameServer.apiPort + index + 1}/server/stop");
                    } catch (Exception) {
                        // ignored
                    }
                }
            }
        }

        public void KillGameServer(GameServer gameServer) {
            if (!gameServer.processId.HasValue) {
                throw new NullReferenceException();
            }

            Process process = Process.GetProcesses().FirstOrDefault(x => x.Id == gameServer.processId.Value);
            if (process != null && !process.HasExited) {
                process.Kill();
            }

            gameServer.processId = null;

            gameServer.headlessClientProcessIds.ForEach(
                x => {
                    process = Process.GetProcesses().FirstOrDefault(y => y.Id == x);
                    if (process != null && !process.HasExited) {
                        process.Kill();
                    }
                }
            );
            gameServer.headlessClientProcessIds.Clear();
        }

        public int KillAllArmaProcesses() {
            List<Process> processes = gameServerHelpers.GetArmaProcesses().ToList();
            foreach (Process process in processes) {
                process.Kill();
            }

            Data.Get()
                .ToList()
                .ForEach(
                    x => {
                        x.processId = null;
                        x.headlessClientProcessIds.Clear();
                    }
                );
            return processes.Count;
        }

        public List<GameServerMod> GetAvailableMods(string id) {
            GameServer gameServer = Data.GetSingle(id);
            Uri serverExecutable = new Uri(gameServerHelpers.GetGameServerExecutablePath(gameServer));
            List<GameServerMod> mods = new List<GameServerMod>();
            IEnumerable<string> availableModsFolders = new[] { gameServerHelpers.GetGameServerModsPaths(gameServer.environment) };
            IEnumerable<string> extraModsFolders = gameServerHelpers.GetGameServerExtraModsPaths();
            availableModsFolders = availableModsFolders.Concat(extraModsFolders);
            foreach (string modsPath in availableModsFolders) {
                IEnumerable<DirectoryInfo> modFolders = new DirectoryInfo(modsPath).EnumerateDirectories("@*", SearchOption.TopDirectoryOnly);
                foreach (DirectoryInfo modFolder in modFolders) {
                    if (mods.Any(x => x.path == modFolder.FullName)) continue;

                    IEnumerable<FileInfo> modFiles = new DirectoryInfo(modFolder.FullName).EnumerateFiles("*.pbo", SearchOption.AllDirectories);
                    if (!modFiles.Any()) continue;

                    GameServerMod mod = new GameServerMod { name = modFolder.Name, path = modFolder.FullName };
                    Uri modFolderUri = new Uri(mod.path);
                    if (serverExecutable.IsBaseOf(modFolderUri)) {
                        mod.pathRelativeToServerExecutable = Uri.UnescapeDataString(serverExecutable.MakeRelativeUri(modFolderUri).ToString());
                    }

                    mods.Add(mod);
                }
            }

            foreach (GameServerMod mod in mods) {
                if (mods.Any(x => x.name == mod.name && x.path != mod.path)) {
                    mod.isDuplicate = true;
                }

                foreach (GameServerMod duplicate in mods.Where(x => x.name == mod.name && x.path != mod.path)) {
                    duplicate.isDuplicate = true;
                }
            }

            return mods;
        }

        public List<GameServerMod> GetEnvironmentMods(GameEnvironment environment) {
            string repoModsFolder = gameServerHelpers.GetGameServerModsPaths(environment);
            IEnumerable<DirectoryInfo> modFolders = new DirectoryInfo(repoModsFolder).EnumerateDirectories("@*", SearchOption.TopDirectoryOnly);
            return modFolders.Select(modFolder => new { modFolder, modFiles = new DirectoryInfo(modFolder.FullName).EnumerateFiles("*.pbo", SearchOption.AllDirectories) })
                             .Where(x => x.modFiles.Any())
                             .Select(x => new GameServerMod { name = x.modFolder.Name, path = x.modFolder.FullName })
                             .ToList();
        }
    }
}
