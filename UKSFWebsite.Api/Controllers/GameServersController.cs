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
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Mission;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.NCO, RoleDefinitions.SR5, RoleDefinitions.COMMAND)]
    public class GameServersController : Controller {
        private readonly IGameServersService gameServersService;
        private readonly ISessionService sessionService;
        private readonly IHubContext<ServersHub, IServersClient> serversHub;

        public GameServersController(ISessionService sessionService, IGameServersService gameServersService, IHubContext<ServersHub, IServersClient> serversHub) {
            this.sessionService = sessionService;
            this.gameServersService = gameServersService;
            this.serversHub = serversHub;
        }

        [HttpGet, Authorize]
        public IActionResult GetGameServers() => Ok(new {servers = gameServersService.Get(), missions = gameServersService.GetMissionFiles(), instanceCount = gameServersService.GetGameInstanceCount()});

        [HttpGet("status/{id}"), Authorize]
        public async Task<IActionResult> GetGameServerStatus(string id) {
            GameServer gameServer = gameServersService.GetSingle(id);
            await gameServersService.GetGameServerStatus(gameServer);
            return Ok(new {gameServer, instanceCount = gameServersService.GetGameInstanceCount()});
        }

        [HttpPost("{check}"), Authorize]
        public IActionResult CheckGameServers(string check, [FromBody] GameServer gameServer = null) {
            return Ok(gameServer != null ? gameServersService.GetSingle(x => x.id != gameServer.id && (x.name == check || x.apiPort.ToString() == check)) : gameServersService.GetSingle(x => x.name == check || x.apiPort.ToString() == check));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddServer([FromBody] GameServer gameServer) {
            await gameServersService.Add(gameServer);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Server added '{gameServer}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditGameServer([FromBody] GameServer gameServer) {
            GameServer oldGameServer = gameServersService.GetSingle(x => x.id == gameServer.id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server '{gameServer.name}' updated:{oldGameServer.Changes(gameServer)}");
            await gameServersService.Update(
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

            return Ok(gameServersService.Get());
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteGameServer(string id) {
            GameServer gameServer = gameServersService.GetSingle(x => x.id == id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server deleted '{gameServer.name}'");
            await gameServersService.Delete(id);

            return Ok(gameServersService.Get());
        }

        [HttpPost("order"), Authorize]
        public async Task<IActionResult> UpdateOrder([FromBody] List<GameServer> newServerOrder) {
            for (int index = 0; index < newServerOrder.Count; index++) {
                GameServer gameServer = newServerOrder[index];
                if (gameServersService.GetSingle(gameServer.id).order != index) {
                    await gameServersService.Update(gameServer.id, "order", index);
                }
            }

            return Ok(gameServersService.Get());
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
                    LogWrapper.AuditLog(sessionService.GetContextId(), $"Uploaded mission '{file.Name}'");
                }
            } catch (Exception exception) {
                return BadRequest(exception);
            }

            return Ok(new {missions = gameServersService.GetMissionFiles(), missionReports});
        }

        [HttpPost("launch/{id}"), Authorize]
        public async Task<IActionResult> LaunchServer(string id, [FromBody] JObject data) {
            Task.WaitAll(gameServersService.Get().Select(x => gameServersService.GetGameServerStatus(x)).ToArray());
            GameServer gameServer = gameServersService.GetSingle(id);
            if (gameServer.status.running) return BadRequest("Server is already running. This shouldn't happen so please contact an admin");
            if (GameServerHelpers.IsMainOpTime()) {
                if (gameServer.serverOption == GameServerOption.SINGLETON) {
                    if (gameServersService.Get(x => x.serverOption != GameServerOption.SINGLETON).Any(x => x.status.started || x.status.running)) {
                        return BadRequest("Server must be launched on its own. Stop the other running servers first");
                    }
                }

                if (gameServersService.Get(x => x.serverOption == GameServerOption.SINGLETON).Any(x => x.status.started || x.status.running)) {
                    return BadRequest("Server cannot be launched whilst main server is running at this time");
                }
            }

            if (gameServersService.Get(x => x.port == gameServer.port).Any(x => x.status.started || x.status.running)) {
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

            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server launched '{missionSelection}' on '{gameServer.name}'");
            return Ok(patchingResult.reports);
        }

        [HttpGet("stop/{id}"), Authorize]
        public async Task<IActionResult> StopServer(string id) {
            GameServer gameServer = gameServersService.GetSingle(id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server stopped '{gameServer.name}'");
            await gameServersService.GetGameServerStatus(gameServer);
            if (!gameServer.status.started && !gameServer.status.running) return BadRequest("Server is not running. This shouldn't happen so please contact an admin");
            await gameServersService.StopGameServer(gameServer);
            await gameServersService.GetGameServerStatus(gameServer);
            return Ok(new {gameServer, instanceCount = gameServersService.GetGameInstanceCount()});
        }

        [HttpGet("kill/{id}"), Authorize]
        public async Task<IActionResult> KillServer(string id) {
            GameServer gameServer = gameServersService.GetSingle(id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server killed '{gameServer.name}'");
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
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Killed {killed} Arma instances");
            return Ok();
        }

        [HttpGet("mods"), Authorize]
        public IActionResult GetAvailableMods() => Ok(gameServersService.GetAvailableMods());

        [HttpPost("mods/{id}"), Authorize]
        public async Task<IActionResult> SetGameServerMods(string id, [FromBody] List<GameServerMod> mods) {
            GameServer gameServer = gameServersService.GetSingle(id);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"Game server '{gameServer.name}' mods updated:{gameServer.mods.Select(x => x.name).Changes(mods.Select(x => x.name))}");
            await gameServersService.Update(id, Builders<GameServer>.Update.Unset(x => x.mods));
            await gameServersService.Update(id, Builders<GameServer>.Update.PushEach(x => x.mods, mods));
            return Ok(gameServersService.GetAvailableMods());
        }

        [HttpGet("disabled"), Authorize]
        public IActionResult GetDisabledState() => Ok(new {state = VariablesWrapper.VariablesService().GetSingle("SERVERS_DISABLED").AsBool()});

        [HttpPost("disabled"), Authorize]
        public async Task<IActionResult> SetDisabledState([FromBody] JObject body) {
            bool state = bool.Parse(body["state"].ToString());
            await VariablesWrapper.VariablesService().Update("SERVERS_DISABLED", state);
            await serversHub.Clients.All.ReceiveDisabledState(state);
            return Ok();
        }
    }
}
