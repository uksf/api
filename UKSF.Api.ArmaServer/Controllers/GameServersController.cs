using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Exceptions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Models.Parameters;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Request;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Nco, Permissions.Servers, Permissions.Command)]
public class GameServersController : ControllerBase
{
    private readonly IGameServerHelpers _gameServerHelpers;
    private readonly IGameServersContext _gameServersContext;
    private readonly IGameServersService _gameServersService;
    private readonly IUksfLogger _logger;
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
        IUksfLogger logger
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

    [HttpGet]
    [Authorize]
    public GameServersDataset GetGameServers()
    {
        return new GameServersDataset
        {
            Servers = _gameServersContext.Get(),
            Missions = _gameServersService.GetMissionFiles(),
            InstanceCount = _gameServersService.GetGameInstanceCount()
        };
    }

    [HttpGet("status/{id}")]
    [Authorize]
    public async Task<GameServerDataset> GetGameServerStatus(string id)
    {
        var gameServer = _gameServersContext.GetSingle(id);
        await _gameServersService.GetGameServerStatus(gameServer);
        return new GameServerDataset { GameServer = gameServer, InstanceCount = _gameServersService.GetGameInstanceCount() };
    }

    [HttpPost("{check}")]
    [Authorize]
    public DomainGameServer CheckGameServers(string check, [FromBody] DomainGameServer gameServer = null)
    {
        if (gameServer != null)
        {
            var safeGameServer = gameServer;
            return _gameServersContext.GetSingle(x => x.Id != safeGameServer.Id && (x.Name == check || x.ApiPort.ToString() == check));
        }

        return _gameServersContext.GetSingle(x => x.Name == check || x.ApiPort.ToString() == check);
    }

    [HttpPut]
    [Authorize]
    public async Task AddServer([FromBody] DomainGameServer gameServer)
    {
        gameServer.Order = _gameServersContext.Get().Count();
        await _gameServersContext.Add(gameServer);

        _logger.LogAudit($"Server added '{gameServer}'");
        SendAnyUpdateIfNotCaller(true);
    }

    [HttpPatch]
    [Authorize]
    public async Task<bool> EditGameServer([FromBody] DomainGameServer gameServer)
    {
        var oldGameServer = _gameServersContext.GetSingle(gameServer.Id);
        _logger.LogAudit($"Game server '{gameServer.Name}' updated:{oldGameServer.Changes(gameServer)}");
        var environmentChanged = false;
        if (oldGameServer.Environment != gameServer.Environment)
        {
            environmentChanged = true;
            gameServer.Mods = _gameServersService.GetEnvironmentMods(gameServer.Environment);
            gameServer.ServerMods = new List<GameServerMod>();
        }

        await _gameServersContext.Update(
            gameServer.Id,
            Builders<DomainGameServer>.Update.Set(x => x.Name, gameServer.Name)
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

        SendServerUpdateIfNotCaller(gameServer.Id);
        return environmentChanged;
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IEnumerable<DomainGameServer>> DeleteGameServer(string id)
    {
        var gameServer = _gameServersContext.GetSingle(id);
        _logger.LogAudit($"Game server deleted '{gameServer.Name}'");
        await _gameServersContext.Delete(id);

        SendAnyUpdateIfNotCaller(true);
        return _gameServersContext.Get();
    }

    [HttpPatch("order")]
    [Authorize]
    public async Task<IEnumerable<DomainGameServer>> UpdateOrder([FromBody] OrderUpdateRequest orderUpdate)
    {
        await _gameServersService.UpdateGameServerOrder(orderUpdate);
        SendAnyUpdateIfNotCaller(true);
        return _gameServersContext.Get();
    }

    [HttpPost("mission")]
    [Authorize]
    [RequestSizeLimit(52428800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
    public async Task<MissionsDataset> UploadMissionFile()
    {
        List<MissionReportDataset> missionReports = new();
        try
        {
            foreach (var file in Request.Form.Files.Where(x => x.Length > 0))
            {
                await _gameServersService.UploadMissionFile(file);
                var missionPatchingResult = await _gameServersService.PatchMissionFile(file.Name);
                missionPatchingResult.Reports = missionPatchingResult.Reports.OrderByDescending(x => x.Error).ToList();
                missionReports.Add(new MissionReportDataset { Mission = file.Name, Reports = missionPatchingResult.Reports });
                _logger.LogAudit($"Uploaded mission '{file.Name}'");
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception);
            throw new BadRequestException(exception.Message); // TODO: Needs better error handling
        }

        var missions = _gameServersService.GetMissionFiles();
        SendMissionsUpdateIfNotCaller(missions);
        return new MissionsDataset { Missions = missions, MissionReports = missionReports };
    }

    [HttpPost("launch/{id}")]
    [Authorize]
    public async Task<List<ValidationReport>> LaunchServer(string id, [FromBody] LaunchServerRequest launchServerRequest)
    {
        Task.WaitAll(_gameServersContext.Get().Select(x => _gameServersService.GetGameServerStatus(x)).ToArray());
        var gameServer = _gameServersContext.GetSingle(id);
        if (gameServer.Status.Running)
        {
            throw new BadRequestException("Server is already running. This shouldn't happen so please contact an admin");
        }

        if (_gameServerHelpers.IsMainOpTime())
        {
            if (gameServer.ServerOption == GameServerOption.Singleton)
            {
                if (_gameServersContext.Get(x => x.ServerOption != GameServerOption.Singleton).Any(x => x.Status.Started || x.Status.Running))
                {
                    throw new BadRequestException("Server must be launched on its own. Stop the other running servers first");
                }
            }

            if (_gameServersContext.Get(x => x.ServerOption == GameServerOption.Singleton).Any(x => x.Status.Started || x.Status.Running))
            {
                throw new BadRequestException("Server cannot be launched whilst main server is running at this time");
            }
        }

        if (_gameServersContext.Get(x => x.Port == gameServer.Port).Any(x => x.Status.Started || x.Status.Running))
        {
            throw new BadRequestException("Server cannot be launched while another server with the same port is running");
        }

        var patchingResult = await _gameServersService.PatchMissionFile(launchServerRequest.MissionName);
        if (!patchingResult.Success)
        {
            patchingResult.Reports = patchingResult.Reports.OrderByDescending(x => x.Error).ToList();
            var error =
                $"{(patchingResult.Reports.Count > 0 ? "Failed to patch mission for the reasons detailed below" : "Failed to patch mission for an unknown reason")}.\n\nContact an admin for help";
            throw new MissionPatchingFailedException(error, new ValidationReportDataset { Reports = patchingResult.Reports });
        }

        _gameServersService.WriteServerConfig(gameServer, patchingResult.PlayerCount, launchServerRequest.MissionName);
        gameServer.Status.Mission = launchServerRequest.MissionName;

        await _gameServersService.LaunchGameServer(gameServer);

        _logger.LogAudit($"Game server launched '{launchServerRequest.MissionName}' on '{gameServer.Name}'");
        SendServerUpdateIfNotCaller(gameServer.Id);
        return patchingResult.Reports;
    }

    [HttpGet("stop/{id}")]
    [Authorize]
    public async Task<GameServerDataset> StopServer(string id)
    {
        var gameServer = _gameServersContext.GetSingle(id);
        _logger.LogAudit($"Game server stopped '{gameServer.Name}'");
        await _gameServersService.GetGameServerStatus(gameServer);
        if (!gameServer.Status.Started && !gameServer.Status.Running)
        {
            throw new BadRequestException("Server is not running. This shouldn't happen so please contact an admin");
        }

        SendServerUpdateIfNotCaller(gameServer.Id);
        await _gameServersService.StopGameServer(gameServer);
        await _gameServersService.GetGameServerStatus(gameServer);
        return new GameServerDataset { GameServer = gameServer, InstanceCount = _gameServersService.GetGameInstanceCount() };
    }

    [HttpGet("kill/{id}")]
    [Authorize]
    public async Task<GameServerDataset> KillServer(string id)
    {
        var gameServer = _gameServersContext.GetSingle(id);
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
        SendServerUpdateIfNotCaller(gameServer.Id);
        return new GameServerDataset { GameServer = gameServer, InstanceCount = _gameServersService.GetGameInstanceCount() };
    }

    [HttpGet("killall")]
    [Authorize]
    public void KillAllArmaProcesses()
    {
        var killed = _gameServersService.KillAllArmaProcesses();
        _logger.LogAudit($"Killed {killed} Arma instances");
        SendAnyUpdateIfNotCaller();
    }

    [HttpGet("{id}/mods")]
    [Authorize]
    public List<GameServerMod> GetAvailableMods(string id)
    {
        return _gameServersService.GetAvailableMods(id);
    }

    [HttpPost("{id}/mods")]
    [Authorize]
    public async Task<List<GameServerMod>> SetGameServerMods(string id, [FromBody] DomainGameServer gameServer)
    {
        var oldGameServer = _gameServersContext.GetSingle(id);
        await _gameServersContext.Update(id, Builders<DomainGameServer>.Update.Unset(x => x.Mods).Unset(x => x.ServerMods));
        await _gameServersContext.Update(id, Builders<DomainGameServer>.Update.Set(x => x.Mods, gameServer.Mods).Set(x => x.ServerMods, gameServer.ServerMods));
        _logger.LogAudit($"Game server '{gameServer.Name}' updated:{oldGameServer.Changes(gameServer)}");
        return _gameServersService.GetAvailableMods(id);
    }

    [HttpGet("{id}/mods/reset")]
    [Authorize]
    public GameServerModsDataset ResetGameServerMods(string id)
    {
        var gameServer = _gameServersContext.GetSingle(id);
        return new GameServerModsDataset
        {
            AvailableMods = _gameServersService.GetAvailableMods(id),
            Mods = _gameServersService.GetEnvironmentMods(gameServer.Environment),
            ServerMods = new List<GameServerMod>()
        };
    }

    [HttpGet("disabled")]
    [Authorize]
    public bool GetDisabledState()
    {
        return _variablesService.GetVariable("SERVER_CONTROL_DISABLED").AsBool();
    }

    [HttpPost("disabled")]
    [Authorize]
    public async Task SetDisabledState([FromBody] SetDisabledStateRequest stateRequest)
    {
        await _variablesContext.Update("SERVER_CONTROL_DISABLED", stateRequest.State);
        await _serversHub.Clients.All.ReceiveDisabledState(stateRequest.State);
    }

    private void SendAnyUpdateIfNotCaller(bool skipRefresh = false)
    {
        if (!GetHubConnectionId(out var connectionId))
        {
            return;
        }

        _ = _serversHub.Clients.All.ReceiveAnyUpdateIfNotCaller(connectionId, skipRefresh);
    }

    private void SendServerUpdateIfNotCaller(string serverId)
    {
        if (!GetHubConnectionId(out var connectionId))
        {
            return;
        }

        _ = _serversHub.Clients.All.ReceiveServerUpdateIfNotCaller(connectionId, serverId);
    }

    private void SendMissionsUpdateIfNotCaller(List<MissionFile> missions)
    {
        if (!GetHubConnectionId(out var connectionId))
        {
            return;
        }

        _ = _serversHub.Clients.All.ReceiveMissionsUpdateIfNotCaller(connectionId, missions);
    }

    private bool GetHubConnectionId(out StringValues connecctionId)
    {
        return HttpContext.Request.Headers.TryGetValue("Hub-Connection-Id", out connecctionId);
    }
}
