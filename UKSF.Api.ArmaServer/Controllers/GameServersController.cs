using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Base;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;

namespace UKSF.Api.ArmaServer.Controllers {
    [Route("[controller]"), Permissions(Permissions.NCO, Permissions.SERVERS, Permissions.COMMAND)]
    public class GameServersController : Controller {
        private readonly IGameServersService gameServersService;
        private readonly IHubContext<ServersHub, IServersClient> serversHub;
        private readonly IVariablesService variablesService;
        private readonly IGameServerHelpers gameServerHelpers;
        private readonly ILogger logger;

        public GameServersController(IGameServersService gameServersService, IHubContext<ServersHub, IServersClient> serversHub, IVariablesService variablesService, IGameServerHelpers gameServerHelpers, ILogger logger) {
            this.gameServersService = gameServersService;
            this.serversHub = serversHub;
            this.variablesService = variablesService;
            this.gameServerHelpers = gameServerHelpers;
            this.logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult GetGameServers() =>
            Ok(new { servers = gameServersService.Data.Get(), missions = gameServersService.GetMissionFiles(), instanceCount = gameServersService.GetGameInstanceCount() });

        [HttpGet("status/{id}"), Authorize]
        public async Task<IActionResult> GetGameServerStatus(string id) {
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            await gameServersService.GetGameServerStatus(gameServer);
            return Ok(new { gameServer, instanceCount = gameServersService.GetGameInstanceCount() });
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
            logger.LogAudit($"Server added '{gameServer}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditGameServer([FromBody] GameServer gameServer) {
            GameServer oldGameServer = gameServersService.Data.GetSingle(x => x.id == gameServer.id);
            logger.LogAudit($"Game server '{gameServer.name}' updated:{oldGameServer.Changes(gameServer)}");
            bool environmentChanged = false;
            if (oldGameServer.environment != gameServer.environment) {
                environmentChanged = true;
                gameServer.mods = gameServersService.GetEnvironmentMods(gameServer.environment);
                gameServer.serverMods = new List<GameServerMod>();
            }

            await gameServersService.Data.Update(
                gameServer.id,
                Builders<GameServer>.Update.Set("name", gameServer.name)
                                    .Set("port", gameServer.port)
                                    .Set("apiPort", gameServer.apiPort)
                                    .Set("numberHeadlessClients", gameServer.numberHeadlessClients)
                                    .Set("profileName", gameServer.profileName)
                                    .Set("hostName", gameServer.hostName)
                                    .Set("password", gameServer.password)
                                    .Set("adminPassword", gameServer.adminPassword)
                                    .Set("environment", gameServer.environment)
                                    .Set("serverOption", gameServer.serverOption)
                                    .Set("mods", gameServer.mods)
                                    .Set("serverMods", gameServer.serverMods)
            );
            return Ok(new { environmentChanged });
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteGameServer(string id) {
            GameServer gameServer = gameServersService.Data.GetSingle(x => x.id == id);
            logger.LogAudit($"Game server deleted '{gameServer.name}'");
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
                    missionReports.Add(new { mission = file.Name, missionPatchingResult.reports });
                    logger.LogAudit($"Uploaded mission '{file.Name}'");
                }
            } catch (Exception exception) {
                logger.LogError(exception);
                return BadRequest(exception);
            }

            return Ok(new { missions = gameServersService.GetMissionFiles(), missionReports });
        }

        [HttpPost("launch/{id}"), Authorize]
        public async Task<IActionResult> LaunchServer(string id, [FromBody] JObject data) {
            Task.WaitAll(gameServersService.Data.Get().Select(x => gameServersService.GetGameServerStatus(x)).ToArray());
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            if (gameServer.status.running) return BadRequest("Server is already running. This shouldn't happen so please contact an admin");
            if (gameServerHelpers.IsMainOpTime()) {
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
                return BadRequest(
                    new {
                        patchingResult.reports,
                        message =
                            $"{(patchingResult.reports.Count > 0 ? "Failed to patch mission for the reasons detailed below" : "Failed to patch mission for an unknown reason")}.\n\nContact an admin for help"
                    }
                );
            }

            // Write config
            gameServersService.WriteServerConfig(gameServer, patchingResult.playerCount, missionSelection);
            gameServer.status.mission = missionSelection;

            // Execute launch
            await gameServersService.LaunchGameServer(gameServer);

            logger.LogAudit($"Game server launched '{missionSelection}' on '{gameServer.name}'");
            return Ok(patchingResult.reports);
        }

        [HttpGet("stop/{id}"), Authorize]
        public async Task<IActionResult> StopServer(string id) {
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            logger.LogAudit($"Game server stopped '{gameServer.name}'");
            await gameServersService.GetGameServerStatus(gameServer);
            if (!gameServer.status.started && !gameServer.status.running) return BadRequest("Server is not running. This shouldn't happen so please contact an admin");
            await gameServersService.StopGameServer(gameServer);
            await gameServersService.GetGameServerStatus(gameServer);
            return Ok(new { gameServer, instanceCount = gameServersService.GetGameInstanceCount() });
        }

        [HttpGet("kill/{id}"), Authorize]
        public async Task<IActionResult> KillServer(string id) {
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            logger.LogAudit($"Game server killed '{gameServer.name}'");
            await gameServersService.GetGameServerStatus(gameServer);
            if (!gameServer.status.started && !gameServer.status.running) return BadRequest("Server is not running. This shouldn't happen so please contact an admin");
            try {
                gameServersService.KillGameServer(gameServer);
            } catch (Exception) {
                return BadRequest("Failed to stop server. Contact an admin");
            }

            await gameServersService.GetGameServerStatus(gameServer);
            return Ok(new { gameServer, instanceCount = gameServersService.GetGameInstanceCount() });
        }

        [HttpGet("killall"), Authorize]
        public IActionResult KillAllArmaProcesses() {
            int killed = gameServersService.KillAllArmaProcesses();
            logger.LogAudit($"Killed {killed} Arma instances");
            return Ok();
        }

        [HttpGet("{id}/mods"), Authorize]
        public IActionResult GetAvailableMods(string id) => Ok(gameServersService.GetAvailableMods(id));

        [HttpPost("{id}/mods"), Authorize]
        public async Task<IActionResult> SetGameServerMods(string id, [FromBody] JObject body) {
            List<GameServerMod> mods = JsonConvert.DeserializeObject<List<GameServerMod>>(body.GetValueFromBody("mods"));
            List<GameServerMod> serverMods = JsonConvert.DeserializeObject<List<GameServerMod>>(body.GetValueFromBody("serverMods"));
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            logger.LogAudit($"Game server '{gameServer.name}' mods updated:{gameServer.mods.Select(x => x.name).Changes(mods.Select(x => x.name))}");
            logger.LogAudit($"Game server '{gameServer.name}' serverMods updated:{gameServer.serverMods.Select(x => x.name).Changes(serverMods.Select(x => x.name))}");
            await gameServersService.Data.Update(id, Builders<GameServer>.Update.Unset(x => x.mods).Unset(x => x.serverMods));
            await gameServersService.Data.Update(id, Builders<GameServer>.Update.Set(x => x.mods, mods).Set(x => x.serverMods, serverMods));
            return Ok(gameServersService.GetAvailableMods(id));
        }

        [HttpGet("{id}/mods/reset"), Authorize]
        public IActionResult ResetGameServerMods(string id) {
            GameServer gameServer = gameServersService.Data.GetSingle(id);
            return Ok(new { availableMods = gameServersService.GetAvailableMods(id), mods = gameServersService.GetEnvironmentMods(gameServer.environment), serverMods = new List<GameServerMod>()});
        }

        [HttpGet("disabled"), Authorize]
        public IActionResult GetDisabledState() => Ok(new { state = variablesService.GetVariable("SERVER_CONTROL_DISABLED").AsBool() });

        [HttpPost("disabled"), Authorize]
        public async Task<IActionResult> SetDisabledState([FromBody] JObject body) {
            bool state = bool.Parse(body["state"].ToString());
            await variablesService.Data.Update("SERVER_CONTROL_DISABLED", state);
            await serversHub.Clients.All.ReceiveDisabledState(state);
            return Ok();
        }
    }
}