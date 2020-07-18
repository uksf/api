using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Game;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Mission;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Game;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Signalr.Hubs.Game;
using UKSF.Common;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.NCO, RoleDefinitions.SERVERS, RoleDefinitions.COMMAND)]
    public class GameServersController : Controller {
        private readonly IGameServersService gameServersService;
        private readonly IHubContext<ServersHub, IServersClient> serversHub;
        private readonly ISessionService sessionService;

        public GameServersController(ISessionService sessionService, IGameServersService gameServersService, IHubContext<ServersHub, IServersClient> serversHub) {
            this.sessionService = sessionService;
            this.gameServersService = gameServersService;
            this.serversHub = serversHub;
        }

        [HttpGet, Authorize]
        public IActionResult GetGameServers() => Ok(new {servers = gameServersService.Data.Get(), missions = gameServersService.GetMissionFiles(), instanceCount = gameServersService.GetGameInstanceCount()});

        [HttpGet("status/{id}"), Authorize]
        public async Task<IActionResult> GetGameServerStatus(string id) {
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            await gameServersService.GetGameServerStatus(gameServer);
            return Ok(new {gameServer, instanceCount = gameServersService.GetGameInstanceCount()});
        }

        [HttpPost("{check}"), Authorize]
        public IActionResult CheckGameServers(string check, [FromBody] GameServer gameServer = null) {
            if (gameServer != null) {
                GameServer safeGameServer = gameServer;
                return Ok(gameServersService.Data.GetSingle(x => x.id != safeGameServer.id && (x.name == check || x.apiPort.ToString() == check)));
            }

            return Ok(gameServersService.Data.GetSingle(x => x.name == check || x.apiPort.ToString() == check));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddServer([FromBody] GameServer gameServer) {
            await gameServersService.Data.Add(gameServer);
            LogWrapper.AuditLog($"Server added '{gameServer}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditGameServer([FromBody] GameServer gameServer) {
            GameServer oldGameServer = gameServersService.Data.GetSingle(x => x.id == gameServer.id);
            LogWrapper.AuditLog($"Game server '{gameServer.name}' updated:{oldGameServer.Changes(gameServer)}");
            await gameServersService.Data
                                    .Update(
                                        gameServer.id,
                                        Builders<GameServer>.Update.Set("name", gameServer.name)
                                                            .Set("port", gameServer.port)
                                                            .Set("apiPort", gameServer.apiPort)
                                                            .Set("numberHeadlessClients", gameServer.numberHeadlessClients)
                                                            .Set("profileName", gameServer.profileName)
                                                            .Set("hostName", gameServer.hostName)
                                                            .Set("password", gameServer.password)
                                                            .Set("adminPassword", gameServer.adminPassword)
                                                            .Set("serverOption", gameServer.serverOption)
                                                            .Set("serverMods", gameServer.serverMods)
                                    );

            return Ok(gameServersService.Data.Get());
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteGameServer(string id) {
            GameServer gameServer = gameServersService.Data.GetSingle(x => x.id == id);
            LogWrapper.AuditLog($"Game server deleted '{gameServer.name}'");
            await gameServersService.Data.Delete(id);

            return Ok(gameServersService.Data.Get());
        }

        [HttpPost("order"), Authorize]
        public async Task<IActionResult> UpdateOrder([FromBody] List<GameServer> newServerOrder) {
            for (int index = 0; index < newServerOrder.Count; index++) {
                GameServer gameServer = newServerOrder[index];
                if (gameServersService.Data.GetSingle(gameServer.id).order != index) {
                    await gameServersService.Data.Update(gameServer.id, "order", index);
                }
            }

            return Ok(gameServersService.Data.Get());
        }

        [HttpPost("mission"), Authorize, RequestSizeLimit(10485760), RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
        public async Task<IActionResult> UploadMissionFile() {
            List<object> missionReports = new List<object>();
            try {
                foreach (IFormFile file in Request.Form.Files.Where(x => x.Length > 0)) {
                    await gameServersService.UploadMissionFile(file);
                    MissionPatchingResult missionPatchingResult = await gameServersService.PatchMissionFile(file.Name);
                    missionPatchingResult.reports = missionPatchingResult.reports.OrderByDescending(x => x.error).ToList();
                    missionReports.Add(new {mission = file.Name, missionPatchingResult.reports});
                    LogWrapper.AuditLog($"Uploaded mission '{file.Name}'");
                }
            } catch (Exception exception) {
                LogWrapper.Log(exception);
                return BadRequest(exception);
            }

            return Ok(new {missions = gameServersService.GetMissionFiles(), missionReports});
        }

        [HttpPost("launch/{id}"), Authorize]
        public async Task<IActionResult> LaunchServer(string id, [FromBody] JObject data) {
            Task.WaitAll(gameServersService.Data.Get().Select(x => gameServersService.GetGameServerStatus(x)).ToArray());
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            if (gameServer.status.running) return BadRequest("Server is already running. This shouldn't happen so please contact an admin");
            if (GameServerHelpers.IsMainOpTime()) {
                if (gameServer.serverOption == GameServerOption.SINGLETON) {
                    if (gameServersService.Data.Get(x => x.serverOption != GameServerOption.SINGLETON).Any(x => x.status.started || x.status.running)) {
                        return BadRequest("Server must be launched on its own. Stop the other running servers first");
                    }
                }

                if (gameServersService.Data.Get(x => x.serverOption == GameServerOption.SINGLETON).Any(x => x.status.started || x.status.running)) {
                    return BadRequest("Server cannot be launched whilst main server is running at this time");
                }
            }

            if (gameServersService.Data.Get(x => x.port == gameServer.port).Any(x => x.status.started || x.status.running)) {
                return BadRequest("Server cannot be launched while another server with the same port is running");
            }

            // Patch mission
            string missionSelection = data["missionName"].ToString();
            MissionPatchingResult patchingResult = await gameServersService.PatchMissionFile(missionSelection);
            if (!patchingResult.success) {
                patchingResult.reports = patchingResult.reports.OrderByDescending(x => x.error).ToList();
                return BadRequest(new {patchingResult.reports, message = $"{(patchingResult.reports.Count > 0 ? "Failed to patch mission for the reasons detailed below" : "Failed to patch mission for an unknown reason")}.\n\nContact an admin for help"});
            }

            // Write config
            gameServersService.WriteServerConfig(gameServer, patchingResult.playerCount, missionSelection);

            // Execute launch
            await gameServersService.LaunchGameServer(gameServer);

            LogWrapper.AuditLog($"Game server launched '{missionSelection}' on '{gameServer.name}'");
            return Ok(patchingResult.reports);
        }

        [HttpGet("stop/{id}"), Authorize]
        public async Task<IActionResult> StopServer(string id) {
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            LogWrapper.AuditLog($"Game server stopped '{gameServer.name}'");
            await gameServersService.GetGameServerStatus(gameServer);
            if (!gameServer.status.started && !gameServer.status.running) return BadRequest("Server is not running. This shouldn't happen so please contact an admin");
            await gameServersService.StopGameServer(gameServer);
            await gameServersService.GetGameServerStatus(gameServer);
            return Ok(new {gameServer, instanceCount = gameServersService.GetGameInstanceCount()});
        }

        [HttpGet("kill/{id}"), Authorize]
        public async Task<IActionResult> KillServer(string id) {
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            LogWrapper.AuditLog($"Game server killed '{gameServer.name}'");
            await gameServersService.GetGameServerStatus(gameServer);
            if (!gameServer.status.started && !gameServer.status.running) return BadRequest("Server is not running. This shouldn't happen so please contact an admin");
            try {
                gameServersService.KillGameServer(gameServer);
            } catch (Exception) {
                return BadRequest("Failed to stop server. Contact an admin");
            }

            await gameServersService.GetGameServerStatus(gameServer);
            return Ok(new {gameServer, instanceCount = gameServersService.GetGameInstanceCount()});
        }

        [HttpGet("killall"), Authorize]
        public IActionResult KillAllArmaProcesses() {
            int killed = gameServersService.KillAllArmaProcesses();
            LogWrapper.AuditLog($"Killed {killed} Arma instances");
            return Ok();
        }

        [HttpGet("mods"), Authorize]
        public IActionResult GetAvailableMods() => Ok(gameServersService.GetAvailableMods());

        [HttpPost("mods/{id}"), Authorize]
        public async Task<IActionResult> SetGameServerMods(string id, [FromBody] List<GameServerMod> mods) {
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            LogWrapper.AuditLog($"Game server '{gameServer.name}' mods updated:{gameServer.mods.Select(x => x.name).Changes(mods.Select(x => x.name))}");
            await gameServersService.Data.Update(id, Builders<GameServer>.Update.Unset(x => x.mods));
            await gameServersService.Data.Update(id, Builders<GameServer>.Update.PushEach(x => x.mods, mods));
            return Ok(gameServersService.GetAvailableMods());
        }

        [HttpGet("disabled"), Authorize]
        public IActionResult GetDisabledState() => Ok(new {state = VariablesWrapper.VariablesDataService().GetSingle("SERVERS_DISABLED").AsBool()});

        [HttpPost("disabled"), Authorize]
        public async Task<IActionResult> SetDisabledState([FromBody] JObject body) {
            bool state = bool.Parse(body["state"].ToString());
            await VariablesWrapper.VariablesDataService().Update("SERVERS_DISABLED", state);
            await serversHub.Clients.All.ReceiveDisabledState(state);
            return Ok();
        }
    }
}
