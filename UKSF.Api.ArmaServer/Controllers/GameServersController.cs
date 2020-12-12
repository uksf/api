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
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.ArmaServer.Controllers {
    [Route("[controller]"), Permissions(Permissions.NCO, Permissions.SERVERS, Permissions.COMMAND)]
    public class GameServersController : Controller {
        private readonly IGameServerHelpers _gameServerHelpers;
        private readonly IGameServersContext _gameServersContext;
        private readonly IGameServersService _gameServersService;
        private readonly ILogger _logger;
        private readonly IHubContext<ServersHub, IServersClient> _serversHub;
        private readonly IVariablesContext _variablesContext;
        private readonly IVariablesService _variablesService;

        public GameServersController(
            IGameServersContext gameServersContext,
            IVariablesContext variablesContext,
            IGameServersService gameServersService,
            IHubContext<ServersHub, IServersClient> serversHub,
            IVariablesService variablesService,
            IGameServerHelpers gameServerHelpers,
            ILogger logger
        ) {
            _gameServersContext = gameServersContext;
            _variablesContext = variablesContext;
            _gameServersService = gameServersService;
            _serversHub = serversHub;
            _variablesService = variablesService;
            _gameServerHelpers = gameServerHelpers;
            _logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult GetGameServers() =>
            Ok(new { servers = _gameServersContext.Get(), missions = _gameServersService.GetMissionFiles(), instanceCount = _gameServersService.GetGameInstanceCount() });

        [HttpGet("status/{id}"), Authorize]
        public async Task<IActionResult> GetGameServerStatus(string id) {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            await _gameServersService.GetGameServerStatus(gameServer);
            return Ok(new { gameServer, instanceCount = _gameServersService.GetGameInstanceCount() });
        }

        [HttpPost("{check}"), Authorize]
        public IActionResult CheckGameServers(string check, [FromBody] GameServer gameServer = null) {
            if (gameServer != null) {
                GameServer safeGameServer = gameServer;
                return Ok(_gameServersContext.GetSingle(x => x.Id != safeGameServer.Id && (x.Name == check || x.ApiPort.ToString() == check)));
            }

            return Ok(_gameServersContext.GetSingle(x => x.Name == check || x.ApiPort.ToString() == check));
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> AddServer([FromBody] GameServer gameServer) {
            await _gameServersContext.Add(gameServer);
            _logger.LogAudit($"Server added '{gameServer}'");
            return Ok();
        }

        [HttpPatch, Authorize]
        public async Task<IActionResult> EditGameServer([FromBody] GameServer gameServer) {
            GameServer oldGameServer = _gameServersContext.GetSingle(x => x.Id == gameServer.Id);
            _logger.LogAudit($"Game server '{gameServer.Name}' updated:{oldGameServer.Changes(gameServer)}");
            bool environmentChanged = false;
            if (oldGameServer.Environment != gameServer.Environment) {
                environmentChanged = true;
                gameServer.Mods = _gameServersService.GetEnvironmentMods(gameServer.Environment);
                gameServer.ServerMods = new List<GameServerMod>();
            }

            await _gameServersContext.Update(
                gameServer.Id,
                Builders<GameServer>.Update.Set("name", gameServer.Name)
                                    .Set("port", gameServer.Port)
                                    .Set("apiPort", gameServer.ApiPort)
                                    .Set("numberHeadlessClients", gameServer.NumberHeadlessClients)
                                    .Set("profileName", gameServer.ProfileName)
                                    .Set("hostName", gameServer.HostName)
                                    .Set("password", gameServer.Password)
                                    .Set("adminPassword", gameServer.AdminPassword)
                                    .Set("environment", gameServer.Environment)
                                    .Set("serverOption", gameServer.ServerOption)
                                    .Set("mods", gameServer.Mods)
                                    .Set("serverMods", gameServer.ServerMods)
            );
            return Ok(new { environmentChanged });
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IActionResult> DeleteGameServer(string id) {
            GameServer gameServer = _gameServersContext.GetSingle(x => x.Id == id);
            _logger.LogAudit($"Game server deleted '{gameServer.Name}'");
            await _gameServersContext.Delete(id);

            return Ok(_gameServersContext.Get());
        }

        [HttpPost("order"), Authorize]
        public async Task<IActionResult> UpdateOrder([FromBody] List<GameServer> newServerOrder) {
            for (int index = 0; index < newServerOrder.Count; index++) {
                GameServer gameServer = newServerOrder[index];
                if (_gameServersContext.GetSingle(gameServer.Id).Order != index) {
                    await _gameServersContext.Update(gameServer.Id, x => x.Order, index);
                }
            }

            return Ok(_gameServersContext.Get());
        }

        [HttpPost("mission"), Authorize, RequestSizeLimit(10485760), RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
        public async Task<IActionResult> UploadMissionFile() {
            List<object> missionReports = new();
            try {
                foreach (IFormFile file in Request.Form.Files.Where(x => x.Length > 0)) {
                    await _gameServersService.UploadMissionFile(file);
                    MissionPatchingResult missionPatchingResult = await _gameServersService.PatchMissionFile(file.Name);
                    missionPatchingResult.Reports = missionPatchingResult.Reports.OrderByDescending(x => x.Error).ToList();
                    missionReports.Add(new { mission = file.Name, reports = missionPatchingResult.Reports });
                    _logger.LogAudit($"Uploaded mission '{file.Name}'");
                }
            } catch (Exception exception) {
                _logger.LogError(exception);
                return BadRequest(exception);
            }

            return Ok(new { missions = _gameServersService.GetMissionFiles(), missionReports });
        }

        [HttpPost("launch/{id}"), Authorize]
        public async Task<IActionResult> LaunchServer(string id, [FromBody] JObject data) {
            Task.WaitAll(_gameServersContext.Get().Select(x => _gameServersService.GetGameServerStatus(x)).ToArray());
            GameServer gameServer = _gameServersContext.GetSingle(id);
            if (gameServer.Status.Running) return BadRequest("Server is already running. This shouldn't happen so please contact an admin");
            if (_gameServerHelpers.IsMainOpTime()) {
                if (gameServer.ServerOption == GameServerOption.SINGLETON) {
                    if (_gameServersContext.Get(x => x.ServerOption != GameServerOption.SINGLETON).Any(x => x.Status.Started || x.Status.Running)) {
                        return BadRequest("Server must be launched on its own. Stop the other running servers first");
                    }
                }

                if (_gameServersContext.Get(x => x.ServerOption == GameServerOption.SINGLETON).Any(x => x.Status.Started || x.Status.Running)) {
                    return BadRequest("Server cannot be launched whilst main server is running at this time");
                }
            }

            if (_gameServersContext.Get(x => x.Port == gameServer.Port).Any(x => x.Status.Started || x.Status.Running)) {
                return BadRequest("Server cannot be launched while another server with the same port is running");
            }

            // Patch mission
            string missionSelection = data["missionName"].ToString();
            MissionPatchingResult patchingResult = await _gameServersService.PatchMissionFile(missionSelection);
            if (!patchingResult.Success) {
                patchingResult.Reports = patchingResult.Reports.OrderByDescending(x => x.Error).ToList();
                return BadRequest(
                    new {
                        reports = patchingResult.Reports,
                        message =
                            $"{(patchingResult.Reports.Count > 0 ? "Failed to patch mission for the reasons detailed below" : "Failed to patch mission for an unknown reason")}.\n\nContact an admin for help"
                    }
                );
            }

            // Write config
            _gameServersService.WriteServerConfig(gameServer, patchingResult.PlayerCount, missionSelection);
            gameServer.Status.Mission = missionSelection;

            // Execute launch
            await _gameServersService.LaunchGameServer(gameServer);

            _logger.LogAudit($"Game server launched '{missionSelection}' on '{gameServer.Name}'");
            return Ok(patchingResult.Reports);
        }

        [HttpGet("stop/{id}"), Authorize]
        public async Task<IActionResult> StopServer(string id) {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            _logger.LogAudit($"Game server stopped '{gameServer.Name}'");
            await _gameServersService.GetGameServerStatus(gameServer);
            if (!gameServer.Status.Started && !gameServer.Status.Running) return BadRequest("Server is not running. This shouldn't happen so please contact an admin");
            await _gameServersService.StopGameServer(gameServer);
            await _gameServersService.GetGameServerStatus(gameServer);
            return Ok(new { gameServer, instanceCount = _gameServersService.GetGameInstanceCount() });
        }

        [HttpGet("kill/{id}"), Authorize]
        public async Task<IActionResult> KillServer(string id) {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            _logger.LogAudit($"Game server killed '{gameServer.Name}'");
            await _gameServersService.GetGameServerStatus(gameServer);
            if (!gameServer.Status.Started && !gameServer.Status.Running) return BadRequest("Server is not running. This shouldn't happen so please contact an admin");
            try {
                _gameServersService.KillGameServer(gameServer);
            } catch (Exception) {
                return BadRequest("Failed to stop server. Contact an admin");
            }

            await _gameServersService.GetGameServerStatus(gameServer);
            return Ok(new { gameServer, instanceCount = _gameServersService.GetGameInstanceCount() });
        }

        [HttpGet("killall"), Authorize]
        public IActionResult KillAllArmaProcesses() {
            int killed = _gameServersService.KillAllArmaProcesses();
            _logger.LogAudit($"Killed {killed} Arma instances");
            return Ok();
        }

        [HttpGet("{id}/mods"), Authorize]
        public IActionResult GetAvailableMods(string id) => Ok(_gameServersService.GetAvailableMods(id));

        [HttpPost("{id}/mods"), Authorize]
        public async Task<IActionResult> SetGameServerMods(string id, [FromBody] JObject body) {
            List<GameServerMod> mods = JsonConvert.DeserializeObject<List<GameServerMod>>(body.GetValueFromBody("mods"));
            List<GameServerMod> serverMods = JsonConvert.DeserializeObject<List<GameServerMod>>(body.GetValueFromBody("serverMods"));
            GameServer gameServer = _gameServersContext.GetSingle(id);
            _logger.LogAudit($"Game server '{gameServer.Name}' mods updated:{gameServer.Mods.Select(x => x.Name).Changes(mods.Select(x => x.Name))}");
            _logger.LogAudit($"Game server '{gameServer.Name}' serverMods updated:{gameServer.ServerMods.Select(x => x.Name).Changes(serverMods.Select(x => x.Name))}");
            await _gameServersContext.Update(id, Builders<GameServer>.Update.Unset(x => x.Mods).Unset(x => x.ServerMods));
            await _gameServersContext.Update(id, Builders<GameServer>.Update.Set(x => x.Mods, mods).Set(x => x.ServerMods, serverMods));
            return Ok(_gameServersService.GetAvailableMods(id));
        }

        [HttpGet("{id}/mods/reset"), Authorize]
        public IActionResult ResetGameServerMods(string id) {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            return Ok(new { availableMods = _gameServersService.GetAvailableMods(id), mods = _gameServersService.GetEnvironmentMods(gameServer.Environment), serverMods = new List<GameServerMod>() });
        }

        [HttpGet("disabled"), Authorize]
        public IActionResult GetDisabledState() => Ok(new { state = _variablesService.GetVariable("SERVER_CONTROL_DISABLED").AsBool() });

        [HttpPost("disabled"), Authorize]
        public async Task<IActionResult> SetDisabledState([FromBody] JObject body) {
            bool state = bool.Parse(body["state"].ToString());
            await _variablesContext.Update("SERVER_CONTROL_DISABLED", state);
            await _serversHub.Clients.All.ReceiveDisabledState(state);
            return Ok();
        }
    }
}
