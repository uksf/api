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
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Exceptions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.ArmaServer.Controllers
{
    [Route("[controller]"), Permissions(Permissions.NCO, Permissions.SERVERS, Permissions.COMMAND)]
    public class GameServersController : ControllerBase
    {
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
        )
        {
            _gameServersContext = gameServersContext;
            _variablesContext = variablesContext;
            _gameServersService = gameServersService;
            _serversHub = serversHub;
            _variablesService = variablesService;
            _gameServerHelpers = gameServerHelpers;
            _logger = logger;
        }

        [HttpGet, Authorize]
        public GameServersDataset GetGameServers()
        {
            return new() { Servers = _gameServersContext.Get(), Missions = _gameServersService.GetMissionFiles(), InstanceCount = _gameServersService.GetGameInstanceCount() };
        }

        [HttpGet("status/{id}"), Authorize]
        public async Task<GameServerDataset> GetGameServerStatus(string id)
        {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            await _gameServersService.GetGameServerStatus(gameServer);
            return new() { GameServer = gameServer, InstanceCount = _gameServersService.GetGameInstanceCount() };
        }

        [HttpPost("{check}"), Authorize]
        public GameServer CheckGameServers(string check, [FromBody] GameServer gameServer = null)
        {
            if (gameServer != null)
            {
                GameServer safeGameServer = gameServer;
                return _gameServersContext.GetSingle(x => x.Id != safeGameServer.Id && (x.Name == check || x.ApiPort.ToString() == check));
            }

            return _gameServersContext.GetSingle(x => x.Name == check || x.ApiPort.ToString() == check);
        }

        [HttpPut, Authorize]
        public async Task AddServer([FromBody] GameServer gameServer)
        {
            await _gameServersContext.Add(gameServer);
            _logger.LogAudit($"Server added '{gameServer}'");
        }

        [HttpPatch, Authorize]
        public async Task<bool> EditGameServer([FromBody] GameServer gameServer)
        {
            GameServer oldGameServer = _gameServersContext.GetSingle(gameServer.Id);
            _logger.LogAudit($"Game server '{gameServer.Name}' updated:{oldGameServer.Changes(gameServer)}");
            bool environmentChanged = false;
            if (oldGameServer.Environment != gameServer.Environment)
            {
                environmentChanged = true;
                gameServer.Mods = _gameServersService.GetEnvironmentMods(gameServer.Environment);
                gameServer.ServerMods = new();
            }

            await _gameServersContext.Update(
                gameServer.Id,
                Builders<GameServer>.Update.Set(x => x.Name, gameServer.Name)
                                    .Set(x => x.Port, gameServer.Port)
                                    .Set(x => x.ApiPort, gameServer.ApiPort)
                                    .Set(x => x.NumberHeadlessClients, gameServer.NumberHeadlessClients)
                                    .Set(x => x.ProfileName, gameServer.ProfileName)
                                    .Set(x => x.HostName, gameServer.HostName)
                                    .Set(x => x.Password, gameServer.Password)
                                    .Set(x => x.AdminPassword, gameServer.AdminPassword)
                                    .Set(x => x.Environment, gameServer.Environment)
                                    .Set(x => x.ServerOption, gameServer.ServerOption)
                                    .Set(x => x.Mods, gameServer.Mods)
                                    .Set(x => x.ServerMods, gameServer.ServerMods)
            );
            return environmentChanged;
        }

        [HttpDelete("{id}"), Authorize]
        public async Task<IEnumerable<GameServer>> DeleteGameServer(string id)
        {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            _logger.LogAudit($"Game server deleted '{gameServer.Name}'");
            await _gameServersContext.Delete(id);

            return _gameServersContext.Get();
        }

        [HttpPost("order"), Authorize]
        public async Task<IEnumerable<GameServer>> UpdateOrder([FromBody] List<GameServer> newServerOrder)
        {
            for (int index = 0; index < newServerOrder.Count; index++)
            {
                GameServer gameServer = newServerOrder[index];
                if (_gameServersContext.GetSingle(gameServer.Id).Order != index)
                {
                    await _gameServersContext.Update(gameServer.Id, x => x.Order, index);
                }
            }

            return _gameServersContext.Get();
        }

        [HttpPost("mission"), Authorize, RequestSizeLimit(10485760), RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
        public async Task<MissionsDataset> UploadMissionFile()
        {
            List<MissionReportDataset> missionReports = new();
            try
            {
                foreach (IFormFile file in Request.Form.Files.Where(x => x.Length > 0))
                {
                    await _gameServersService.UploadMissionFile(file);
                    MissionPatchingResult missionPatchingResult = await _gameServersService.PatchMissionFile(file.Name);
                    missionPatchingResult.Reports = missionPatchingResult.Reports.OrderByDescending(x => x.Error).ToList();
                    missionReports.Add(new() { Mission = file.Name, Reports = missionPatchingResult.Reports });
                    _logger.LogAudit($"Uploaded mission '{file.Name}'");
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception);
                throw new BadRequestException(exception.Message); // TODO: Needs better error handling
            }

            return new() { Missions = _gameServersService.GetMissionFiles(), MissionReports = missionReports };
        }

        [HttpPost("launch/{id}"), Authorize]
        public async Task<List<ValidationReport>> LaunchServer(string id, [FromBody] JObject data)
        {
            Task.WaitAll(_gameServersContext.Get().Select(x => _gameServersService.GetGameServerStatus(x)).ToArray());
            GameServer gameServer = _gameServersContext.GetSingle(id);
            if (gameServer.Status.Running)
            {
                throw new BadRequestException("Server is already running. This shouldn't happen so please contact an admin");
            }

            if (_gameServerHelpers.IsMainOpTime())
            {
                if (gameServer.ServerOption == GameServerOption.SINGLETON)
                {
                    if (_gameServersContext.Get(x => x.ServerOption != GameServerOption.SINGLETON).Any(x => x.Status.Started || x.Status.Running))
                    {
                        throw new BadRequestException("Server must be launched on its own. Stop the other running servers first");
                    }
                }

                if (_gameServersContext.Get(x => x.ServerOption == GameServerOption.SINGLETON).Any(x => x.Status.Started || x.Status.Running))
                {
                    throw new BadRequestException("Server cannot be launched whilst main server is running at this time");
                }
            }

            if (_gameServersContext.Get(x => x.Port == gameServer.Port).Any(x => x.Status.Started || x.Status.Running))
            {
                throw new BadRequestException("Server cannot be launched while another server with the same port is running");
            }

            string missionSelection = data["missionName"].ToString();
            MissionPatchingResult patchingResult = await _gameServersService.PatchMissionFile(missionSelection);
            if (!patchingResult.Success)
            {
                patchingResult.Reports = patchingResult.Reports.OrderByDescending(x => x.Error).ToList();
                string error =
                    $"{(patchingResult.Reports.Count > 0 ? "Failed to patch mission for the reasons detailed below" : "Failed to patch mission for an unknown reason")}.\n\nContact an admin for help";
                throw new MissionPatchingFailedException(error, new() { Reports = patchingResult.Reports });
            }

            _gameServersService.WriteServerConfig(gameServer, patchingResult.PlayerCount, missionSelection);
            gameServer.Status.Mission = missionSelection;

            await _gameServersService.LaunchGameServer(gameServer);

            _logger.LogAudit($"Game server launched '{missionSelection}' on '{gameServer.Name}'");
            return patchingResult.Reports;
        }

        [HttpGet("stop/{id}"), Authorize]
        public async Task<GameServerDataset> StopServer(string id)
        {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            _logger.LogAudit($"Game server stopped '{gameServer.Name}'");
            await _gameServersService.GetGameServerStatus(gameServer);
            if (!gameServer.Status.Started && !gameServer.Status.Running)
            {
                throw new BadRequestException("Server is not running. This shouldn't happen so please contact an admin");
            }

            await _gameServersService.StopGameServer(gameServer);
            await _gameServersService.GetGameServerStatus(gameServer);
            return new() { GameServer = gameServer, InstanceCount = _gameServersService.GetGameInstanceCount() };
        }

        [HttpGet("kill/{id}"), Authorize]
        public async Task<GameServerDataset> KillServer(string id)
        {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            _logger.LogAudit($"Game server killed '{gameServer.Name}'");
            await _gameServersService.GetGameServerStatus(gameServer);
            if (!gameServer.Status.Started && !gameServer.Status.Running)
            {
                throw new BadRequestException("Server is not running. This shouldn't happen so please contact an admin");
            }

            try
            {
                _gameServersService.KillGameServer(gameServer);
            }
            catch (Exception)
            {
                throw new BadRequestException("Failed to stop server. Contact an admin");
            }

            await _gameServersService.GetGameServerStatus(gameServer);
            return new() { GameServer = gameServer, InstanceCount = _gameServersService.GetGameInstanceCount() };
        }

        [HttpGet("killall"), Authorize]
        public void KillAllArmaProcesses()
        {
            int killed = _gameServersService.KillAllArmaProcesses();
            _logger.LogAudit($"Killed {killed} Arma instances");
        }

        [HttpGet("{id}/mods"), Authorize]
        public List<GameServerMod> GetAvailableMods(string id)
        {
            return _gameServersService.GetAvailableMods(id);
        }

        [HttpPost("{id}/mods"), Authorize]
        public async Task<List<GameServerMod>> SetGameServerMods(string id, [FromBody] GameServer gameServer)
        {
            GameServer oldGameServer = _gameServersContext.GetSingle(id);
            await _gameServersContext.Update(id, Builders<GameServer>.Update.Unset(x => x.Mods).Unset(x => x.ServerMods));
            await _gameServersContext.Update(id, Builders<GameServer>.Update.Set(x => x.Mods, gameServer.Mods).Set(x => x.ServerMods, gameServer.ServerMods));
            _logger.LogAudit($"Game server '{gameServer.Name}' updated:{oldGameServer.Changes(gameServer)}");
            return _gameServersService.GetAvailableMods(id);
        }

        [HttpGet("{id}/mods/reset"), Authorize]
        public GameServerModsDataset ResetGameServerMods(string id)
        {
            GameServer gameServer = _gameServersContext.GetSingle(id);
            return new() { AvailableMods = _gameServersService.GetAvailableMods(id), Mods = _gameServersService.GetEnvironmentMods(gameServer.Environment), ServerMods = new() };
        }

        [HttpGet("disabled"), Authorize]
        public bool GetDisabledState()
        {
            return _variablesService.GetVariable("SERVER_CONTROL_DISABLED").AsBool();
        }

        [HttpPost("disabled"), Authorize]
        public async Task SetDisabledState([FromBody] JObject body)
        {
            bool state = bool.Parse(body["state"].ToString());
            await _variablesContext.Update("SERVER_CONTROL_DISABLED", state);
            await _serversHub.Clients.All.ReceiveDisabledState(state);
        }
    }
}
