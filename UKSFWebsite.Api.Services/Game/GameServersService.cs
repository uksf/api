using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Game;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Models.Game;
using UKSFWebsite.Api.Models.Mission;
using UKSFWebsite.Api.Services.Utility;
using UKSFWebsite.Api.Signalr.Hubs.Game;

namespace UKSFWebsite.Api.Services.Game {
    public class GameServersService : DataBackedService<IGameServersDataService>, IGameServersService {
        private readonly IHubContext<GameServerHub, IGameServerClient> gameServerHub;
        private readonly IHubContext<GameServersHub, IGameServersClient> gameServersHub;
        private readonly IMissionPatchingService missionPatchingService;
        private readonly ConcurrentDictionary<string, GameServerStatus> serverStatuses = new ConcurrentDictionary<string, GameServerStatus>();

        public GameServersService(IGameServersDataService data, IMissionPatchingService missionPatchingService, IHubContext<GameServersHub, IGameServersClient> gameServersHub, IHubContext<GameServerHub, IGameServerClient> gameServerHub) : base(data) {
            this.missionPatchingService = missionPatchingService;
            this.gameServersHub = gameServersHub;
            this.gameServerHub = gameServerHub;
        }

        public int GetGameInstanceCount() => GameServerHelpers.GetArmaProcesses().Count();

        public async Task<bool> UploadMissionFile(IFormFile file) {
            string fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
            if (IsServerRunning(x => x.Value.mission == fileName)) return false;
            await using FileStream stream = new FileStream(Path.Combine(GameServerHelpers.GetGameServerMissionsPath(), fileName), FileMode.Create);
            await file.CopyToAsync(stream);
            return true;
        }

        public List<MissionFile> GetMissionFiles() {
            IEnumerable<FileInfo> files = new DirectoryInfo(GameServerHelpers.GetGameServerMissionsPath()).EnumerateFiles("*.pbo", SearchOption.TopDirectoryOnly);
            return files.Select(fileInfo => new MissionFile(fileInfo)).OrderBy(x => x.map).ThenBy(x => x.name).ToList();
        }

        public async Task<MissionPatchingResult> PatchMissionFile(string missionName) {
            string missionPath = Path.Combine(GameServerHelpers.GetGameServerMissionsPath(), missionName);
            MissionPatchingResult result = await missionPatchingService.PatchMission(missionPath);
            return result;
        }

        public void WriteServerConfig(GameServer gameServer, int playerCount, string missionSelection) => File.WriteAllText(gameServer.GetGameServerConfigPath(), gameServer.FormatGameServerConfig(playerCount, missionSelection));

        public async Task LaunchGameServer(GameServer gameServer) {
            string launchArguments = gameServer.FormatGameServerLaunchArguments();
            int processId = ProcessHelper.LaunchManagedProcess(GameServerHelpers.GetGameServerExecutablePath(), launchArguments);
            await UpdateGameServerStatus(new GameServerStatus {port = gameServer.port, name = gameServer.name, processId = processId});

            if (gameServer.numberHeadlessClients > 0) {
                for (int index = 0; index < gameServer.numberHeadlessClients; index++) {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    launchArguments = gameServer.FormatHeadlessClientLaunchArguments(index);
                    processId = ProcessHelper.LaunchManagedProcess(GameServerHelpers.GetGameServerExecutablePath(), launchArguments);
                    string name = GameServerHelpers.GetHeadlessClientName(index);
                    await UpdateGameServerStatus(new GameServerStatus {port = gameServer.port, type = GameServerType.HEADLESS, name = name, processId = processId});
                }
            }
        }

        public async Task StopGameServer(GameServer gameServer) {
            await gameServerHub.Clients.Group(gameServer.Key()).Shutdown();

            if (gameServer.numberHeadlessClients > 0) {
                for (int index = 0; index < gameServer.numberHeadlessClients; index++) {
                    string name = GameServerHelpers.GetHeadlessClientName(index);
                    await gameServerHub.Clients.Group(gameServer.HeadlessClientKey(name)).Shutdown();
                }
            }
        }

        public async Task KillGameServer(GameServer gameServer) {
            if (serverStatuses.ContainsKey(gameServer.Key())) {
                GameServerStatus serverStatus = serverStatuses[gameServer.Key()];
                Process process = Process.GetProcesses().FirstOrDefault(x => x.Id == serverStatus.processId);
                if (process != null && !process.HasExited) {
                    process.Kill();
                }

                await RemoveGameServerStatus(serverStatus.Key());
            }

            if (gameServer.numberHeadlessClients > 0) {
                for (int index = 0; index < gameServer.numberHeadlessClients; index++) {
                    string name = GameServerHelpers.GetHeadlessClientName(index);
                    if (serverStatuses.ContainsKey(gameServer.HeadlessClientKey(name))) {
                        GameServerStatus serverStatus = serverStatuses[gameServer.HeadlessClientKey(name)];
                        Process process = Process.GetProcesses().FirstOrDefault(x => x.Id == serverStatus.processId);
                        if (process != null && !process.HasExited) {
                            process.Kill();
                        }

                        await RemoveGameServerStatus(serverStatus.Key());
                    }
                }
            }
        }

        public async Task<int> KillAllArmaProcesses() {
            List<Process> processes = GameServerHelpers.GetArmaProcesses().ToList();
            foreach (Process process in processes) {
                process.Kill();
            }

            await ClearGameServerStatuses();

            return processes.Count;
        }

        public async Task UpdateGameServerStatus(GameServerStatus gameServerStatus) {
            serverStatuses[gameServerStatus.Key()] = gameServerStatus;
            await gameServersHub.Clients.Group(gameServerStatus.Key()).ReceiveServerStatusUpdate(gameServerStatus);
            await gameServersHub.Clients.Group(GameServersHub.ALL).ReceiveServerStatusUpdate(gameServerStatus);
        }

        public GameServerStatus GetStatus(string key) => serverStatuses.ContainsKey(key) ? serverStatuses[key] : null;

        public List<GameServerStatus> GetAllStatuses() => serverStatuses.Values.ToList();

        public bool IsServerRunning(GameServer gameServer) {
            GameServerStatus status = GetStatus(gameServer.Key());
            if (status == null) return false;
            // TODO: Investigate state numbers
            return true;
        }

        public bool IsServerRunning(Func<KeyValuePair<string, GameServerStatus>, bool> predicate) => serverStatuses.Any(predicate);

        public List<GameServerMod> GetAvailableMods() {
            Uri serverExecutable = new Uri(GameServerHelpers.GetGameServerExecutablePath());
            List<GameServerMod> mods = new List<GameServerMod>();
            foreach (string modsPath in GameServerHelpers.GetGameServerModsPaths()) {
                IEnumerable<DirectoryInfo> folders = new DirectoryInfo(modsPath).EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
                foreach (DirectoryInfo folder in folders) {
                    if (!Directory.Exists(Path.Combine(folder.FullName, "addons"))) continue; 
                    IEnumerable<FileInfo> modFiles = new DirectoryInfo(Path.Combine(folder.FullName, "addons")).EnumerateFiles("*.pbo", SearchOption.AllDirectories);
                    if (!modFiles.Any()) continue;
                    GameServerMod mod = new GameServerMod {name = folder.Name, path = folder.FullName};
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

        public async Task RemoveGameServerStatus(string key) {
            if (!serverStatuses.ContainsKey(key)) return;
            serverStatuses.Remove(key, out GameServerStatus unused);
            await gameServersHub.Clients.Group(key).ReceiveServerStatusRemoved(key);
            await gameServersHub.Clients.Group(GameServersHub.ALL).ReceiveServerStatusRemoved(key);
        }

        private async Task ClearGameServerStatuses() {
            serverStatuses.Clear();
            await gameServersHub.Clients.Group(GameServersHub.ALL).ReceiveServerStatusesCleared();
        }
    }
}
