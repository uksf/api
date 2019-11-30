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
using UKSFWebsite.Api.Interfaces.Game;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Game;
using UKSFWebsite.Api.Models.Mission;
using UKSFWebsite.Api.Services.Admin;
using UKSFWebsite.Api.Services.Game;
using UKSFWebsite.Api.Services.Message;
using UKSFWebsite.Api.Services.Personnel;
using UKSFWebsite.Api.Services.Utility;
using UKSFWebsite.Api.Signalr.Hubs.Game;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.NCO, RoleDefinitions.SR5, RoleDefinitions.COMMAND)]
    public class GameServersController : Controller {
        private readonly IGameServersService gameServersService;
        private readonly IHubContext<GameServersHub, IGameServersClient> serversHub;
        private readonly ISessionService sessionService;

        public GameServersController(ISessionService sessionService, IGameServersService gameServersService, IHubContext<GameServersHub, IGameServersClient> serversHub) {
            this.sessionService = sessionService;
            this.gameServersService = gameServersService;
            this.serversHub = serversHub;
        }

        [HttpGet, Authorize]
        public IActionResult GetGameServers() => Ok(new {servers = gameServersService.Data().Get(), statuses = gameServersService.GetAllStatuses(), missions = gameServersService.GetMissionFiles()});

        [HttpGet("status/{id}"), Authorize]
        public IActionResult GetGameServerStatus(string id) {
            GameServer gameServer = gameServersService.Data().GetSingle(id);
            GameServerStatus status = gameServersService.GetStatus(gameServer.Key());
            return Ok(new {gameServer, status});
        }

        [HttpPost("{check}"), Authorize]
        public IActionResult CheckGameServers(string check, [FromBody] GameServer gameServer = null) {
            if (gameServer != null) {
                GameServer safeGameServer = gameServer;
                return Ok(gameServersService.Data().GetSingle(x => x.id != safeGameServer.id && x.name == check));
            }

            return Ok(gameServersService.Data().GetSingle(x => x.name == check));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddServer([FromBody] GameServer gameServer) {
            await gameServersService.Data().Add(gameServer);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Server added '{gameServer}'");
            await serversHub.Clients.Group(GameServersHub.ALL).ReceiveServerAdded(gameServer);
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditGameServer([FromBody] GameServer gameServer) {
            GameServer oldGameServer = gameServersService.Data().GetSingle(x => x.id == gameServer.id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server '{gameServer.name}' updated:{oldGameServer.Changes(gameServer)}");
            await gameServersService.Data()
                                    .Update(
                                        gameServer.id,
                                        Builders<GameServer>.Update.Set("name", gameServer.name)
                                                            .Set("port", gameServer.port)
                                                            .Set("numberHeadlessClients", gameServer.numberHeadlessClients)
                                                            .Set("profileName", gameServer.profileName)
                                                            .Set("hostName", gameServer.hostName)
                                                            .Set("password", gameServer.password)
                                                            .Set("adminPassword", gameServer.adminPassword)
                                                            .Set("serverOption", gameServer.serverOption)
                                                            .Set("serverMods", gameServer.serverMods)
                                    );

            await serversHub.Clients.Group(gameServer.Key()).ReceiveServerUpdate(gameServer);
            await serversHub.Clients.Group(GameServersHub.ALL).ReceiveServerUpdate(gameServer);
            return Ok();
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteGameServer(string id) {
            GameServer gameServer = gameServersService.Data().GetSingle(x => x.id == id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server deleted '{gameServer.name}'");
            await gameServersService.Data().Delete(id);

            await serversHub.Clients.Group(gameServer.Key()).ReceiveServerRemoved(gameServer.Key());
            await serversHub.Clients.Group(GameServersHub.ALL).ReceiveServerRemoved(gameServer.Key());
            return Ok();
        }

        [HttpPost("order"), Authorize]
        public async Task<IActionResult> UpdateOrder([FromBody] List<GameServer> newServerOrder) {
            for (int index = 0; index < newServerOrder.Count; index++) {
                GameServer gameServer = newServerOrder[index];
                if (gameServersService.Data().GetSingle(gameServer.id).order != index) {
                    await gameServersService.Data().Update(gameServer.id, "order", index);
                }
            }
            
            await serversHub.Clients.Group(GameServersHub.ALL).ReceiveServersUpdate(gameServersService.Data().Get());
            return Ok();
        }

        [HttpPost("mission"), Authorize, RequestSizeLimit(10485760), RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
        public async Task<IActionResult> UploadMissionFile() {
            List<object> missionReports = new List<object>();
            try {
                foreach (IFormFile file in Request.Form.Files.Where(x => x.Length > 0)) {
                    if (!await gameServersService.UploadMissionFile(file)) {
                        MissionPatchingResult missionPatchingResult = new MissionPatchingResult {reports = new List<MissionPatchingReport> {new MissionPatchingReport("Mission in use", $"A server using the mission {file.Name} is currently running", true)}};
                        missionReports.Add(new {mission = file.Name, missionPatchingResult.reports});
                    } else {
                        MissionPatchingResult missionPatchingResult = await gameServersService.PatchMissionFile(file.Name);
                        missionPatchingResult.reports = missionPatchingResult.reports.OrderByDescending(x => x.error).ToList();
                        missionReports.Add(new {mission = file.Name, missionPatchingResult.reports});
                        LogWrapper.AuditLog(sessionService.GetContextId(), $"Uploaded mission '{file.Name}'");
                    }
                }
            } catch (Exception exception) {
                return BadRequest(exception);
            }

            return Ok(new {missions = gameServersService.GetMissionFiles(), missionReports});
        }

        [HttpPost("launch/{id}"), Authorize]
        public async Task<IActionResult> LaunchServer(string id, [FromBody] JObject data) {
            GameServer gameServer = gameServersService.Data().GetSingle(id);
            GameServerStatus status = gameServersService.GetStatus(gameServer.Key());
            if (status != null) return BadRequest("Server is already running. This shouldn't happen so please contact an admin");
            if (GameServerHelpers.IsMainOpTime()) {
                if (gameServer.serverOption == GameServerOption.SINGLETON) {
                    if (gameServersService.Data().Get(x => x.serverOption != GameServerOption.SINGLETON).Any(x => gameServersService.IsServerRunning(x))) {
                        return BadRequest("Server is defined to only run on its own. Stop the other running servers first");
                    }
                }

                if (gameServersService.Data().Get(x => x.serverOption == GameServerOption.SINGLETON).Any(x => gameServersService.IsServerRunning(x))) {
                    return BadRequest("A server defined to run on its own is already running. Stop that server first");
                }
            }

            if (gameServersService.Data().Get(x => x.port == gameServer.port).Any(x => gameServersService.IsServerRunning(x))) {
                return BadRequest("A server with the same port is already running");
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

            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server launched '{missionSelection}' on '{gameServer.name}'");
            return Ok(patchingResult.reports);
        }

        [HttpGet("stop/{id}"), Authorize]
        public async Task<IActionResult> StopServer(string id) {
            GameServer gameServer = gameServersService.Data().GetSingle(id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server stopped '{gameServer.name}'");
            if (!gameServersService.IsServerRunning(gameServer)) return BadRequest("Server is not running. This shouldn't happen so please contact an admin");
            await gameServersService.StopGameServer(gameServer);
            return Ok();
        }

        [HttpGet("kill/{id}"), Authorize]
        public async Task<IActionResult> KillServer(string id) {
            GameServer gameServer = gameServersService.Data().GetSingle(id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server killed '{gameServer.name}'");
            if (!gameServersService.IsServerRunning(gameServer)) return BadRequest("Server is not running. This shouldn't happen so please contact an admin");
            try {
                await gameServersService.KillGameServer(gameServer);
            } catch (Exception) {
                return BadRequest("Failed to stop server. Contact an admin");
            }

            return Ok();
        }

        [HttpGet("killall"), Authorize]
        public async Task<IActionResult> KillAllArmaProcesses() {
            int killed = await gameServersService.KillAllArmaProcesses();
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Killed {killed} Arma instances");
            return Ok();
        }

        [HttpGet("mods"), Authorize]
        public IActionResult GetAvailableMods() => Ok(gameServersService.GetAvailableMods());

        [HttpPost("mods/{id}"), Authorize]
        public async Task<IActionResult> SetGameServerMods(string id, [FromBody] List<GameServerMod> mods) {
            GameServer gameServer = gameServersService.Data().GetSingle(id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server '{gameServer.name}' mods updated:{gameServer.mods.Select(x => x.name).Changes(mods.Select(x => x.name))}");
            await gameServersService.Data().Update(id, Builders<GameServer>.Update.Unset(x => x.mods));
            await gameServersService.Data().Update(id, Builders<GameServer>.Update.PushEach(x => x.mods, mods));
            return Ok();
        }

        [HttpGet("disabled"), Authorize]
        public IActionResult GetDisabledState() => Ok(VariablesWrapper.VariablesDataService().GetSingle("SERVERS_DISABLED").AsBool());

        [HttpPost("disable"), Authorize]
        public async Task<IActionResult> SetDisabledState([FromBody] JObject body) {
            bool state = bool.Parse(body["state"].ToString());
            await VariablesWrapper.VariablesDataService().Update("SERVERS_DISABLED", state);
            await serversHub.Clients.All.ReceiveDisabledState(state);
            return Ok();
        }
    }
}
