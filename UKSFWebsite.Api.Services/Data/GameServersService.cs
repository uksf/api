using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using Newtonsoft.Json;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Mission;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Data {
    public class GameServersService : CachedDataService<GameServer>, IGameServersService {
        private readonly IMissionPatchingService missionPatchingService;

        public GameServersService(IMongoDatabase database, IMissionPatchingService missionPatchingService) : base(database, "gameServers") => this.missionPatchingService = missionPatchingService;

        public override List<GameServer> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.order).ToList();
            return Collection;
        }

        public int GetGameInstanceCount() => GameServerHelpers.GetArmaProcesses().Count();

        public async Task UploadMissionFile(IFormFile file) {
            string fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
            using (FileStream stream = new FileStream(Path.Combine(GameServerHelpers.GetGameServerMissionsPath(), fileName), FileMode.Create)) {
                await file.CopyToAsync(stream);
            }
        }

        public List<MissionFile> GetMissionFiles() {
            IEnumerable<FileInfo> files = new DirectoryInfo(GameServerHelpers.GetGameServerMissionsPath()).EnumerateFiles("*.pbo", SearchOption.TopDirectoryOnly);
            return files.Select(fileInfo => new MissionFile(fileInfo)).OrderBy(x => x.map).ThenBy(x => x.name).ToList();
        }

        public async Task GetGameServerStatus(GameServer gameServer) {
            if (gameServer.processId != 0) {
                gameServer.status.started = Process.GetProcesses().Any(x => x.Id == gameServer.processId);
                if (!gameServer.status.started) {
                    gameServer.processId = 0;
                }
            }

            using (HttpClient client = new HttpClient()) {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                try {
                    HttpResponseMessage response = await client.GetAsync($"http://localhost:{gameServer.apiPort}/server");
                    if (!response.IsSuccessStatusCode) {
                        gameServer.status.running = false;
                    }

                    string content = await response.Content.ReadAsStringAsync();
                    gameServer.status = JsonConvert.DeserializeObject<GameServerStatus>(content);
                    gameServer.status.parsedUptime = TimeSpan.FromSeconds(gameServer.status.uptime).StripMilliseconds().ToString();
                    gameServer.status.maxPlayers = gameServer.GetMaxPlayerCountFromConfig();
                    gameServer.status.running = true;
                    gameServer.status.started = false;
                } catch (Exception) {
                    gameServer.status.running = false;
                }
            }
        }

        public async Task<MissionPatchingResult> PatchMissionFile(string missionName) {
            string missionPath = Path.Combine(GameServerHelpers.GetGameServerMissionsPath(), missionName);
            MissionPatchingResult result = await missionPatchingService.PatchMission(missionPath);
            return result;
        }

        public void WriteServerConfig(GameServer gameServer, int playerCount, string missionSelection) => File.WriteAllText(gameServer.GetGameServerConfigPath(), gameServer.FormatGameServerConfig(playerCount, missionSelection));

        public async Task LaunchGameServer(GameServer gameServer) {
            string launchArguments = gameServer.FormatGameServerLaunchArguments();
            using (ManagementClass managementClass = new ManagementClass("Win32_Process")) {
                ManagementClass processInfo = new ManagementClass("Win32_ProcessStartup");
                processInfo.Properties["CreateFlags"].Value = 0x00000008;

                ManagementBaseObject inParameters = managementClass.GetMethodParameters("Create");
                inParameters["CommandLine"] = $"\"{GameServerHelpers.GetGameServerExecutablePath()}\" {launchArguments}";
                inParameters["ProcessStartupInformation"] = processInfo;

                ManagementBaseObject result = managementClass.InvokeMethod("Create", inParameters, null);
                if (result != null && (uint) result.Properties["ReturnValue"].Value == 0) {
                    gameServer.processId = (uint) result.Properties["ProcessId"].Value;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            // launch headless clients
            if (gameServer.numberHeadlessClients > 0) {
                for (int index = 0; index < gameServer.numberHeadlessClients; index++) {
                    launchArguments = gameServer.FormatHeadlessClientLaunchArguments(index);
                    using (ManagementClass managementClass = new ManagementClass("Win32_Process")) {
                        ManagementClass processInfo = new ManagementClass("Win32_ProcessStartup");
                        processInfo.Properties["CreateFlags"].Value = 0x00000008;

                        ManagementBaseObject inParameters = managementClass.GetMethodParameters("Create");
                        inParameters["CommandLine"] = $"\"{GameServerHelpers.GetGameServerExecutablePath()}\" {launchArguments}";
                        inParameters["ProcessStartupInformation"] = processInfo;

                        ManagementBaseObject result = managementClass.InvokeMethod("Create", inParameters, null);
                        if (result != null && (uint) result.Properties["ReturnValue"].Value == 0) {
                            gameServer.headlessClientProcessIds.Add((uint) result.Properties["ProcessId"].Value);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }

        public async Task StopGameServer(GameServer gameServer) {
            try {
                using (HttpClient client = new HttpClient()) {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    await client.GetAsync($"http://localhost:{gameServer.apiPort}/server/stop");
                }
            } catch (Exception) {
                // ignored
            }

            if (gameServer.numberHeadlessClients > 0) {
                for (int index = 0; index < gameServer.numberHeadlessClients; index++) {
                    try {
                        using (HttpClient client = new HttpClient()) {
                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            await client.GetAsync($"http://localhost:{gameServer.apiPort + index + 1}/server/stop");
                        }
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
            List<Process> processes = GameServerHelpers.GetArmaProcesses().ToList();
            foreach (Process process in processes) {
                process.Kill();
            }

            Get()
                .ForEach(
                    x => {
                        x.processId = null;
                        x.headlessClientProcessIds.Clear();
                    }
                );
            return processes.Count;
        }

        public List<GameServerMod> GetAvailableMods() {
            Uri serverExecutable = new Uri(GameServerHelpers.GetGameServerExecutablePath());
            List<GameServerMod> mods = new List<GameServerMod>();
            foreach (string modsPath in GameServerHelpers.GetGameServerModsPaths()) {
                IEnumerable<DirectoryInfo> folders = new DirectoryInfo(modsPath).EnumerateDirectories("@*", SearchOption.TopDirectoryOnly);
                foreach (DirectoryInfo folder in folders) {
                    IEnumerable<FileInfo> modFiles = new DirectoryInfo(folder.FullName).EnumerateFiles("*.pbo", SearchOption.AllDirectories);
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
    }
}
