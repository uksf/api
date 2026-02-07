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
public class GameServersController(
    IGameServersContext gameServersContext,
    IVariablesContext variablesContext,
    IGameServersService gameServersService,
    IHubContext<ServersHub, IServersClient> serversHub,
    IVariablesService variablesService,
    IGameServerHelpers gameServerHelpers,
    IUksfLogger logger,
    IHttpContextService httpContextService
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public GameServersDataset GetGameServers()
    {
        return new GameServersDataset
        {
            Servers = gameServersContext.Get(),
            Missions = gameServersService.GetMissionFiles(),
            InstanceCount = gameServersService.GetGameInstanceCount()
        };
    }

    [HttpGet("status/{id}")]
    [Authorize]
    public async Task<GameServerDataset> GetGameServerStatus(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        await gameServersService.GetGameServerStatus(gameServer);
        return new GameServerDataset { GameServer = gameServer, InstanceCount = gameServersService.GetGameInstanceCount() };
    }

    [HttpPost("{check}")]
    [Authorize]
    public DomainGameServer CheckGameServers(string check, [FromBody] DomainGameServer gameServer = null)
    {
        if (gameServer is not null)
        {
            var safeGameServer = gameServer;
            return gameServersContext.GetSingle(x => x.Id != safeGameServer.Id && (x.Name == check || x.ApiPort.ToString() == check));
        }

        return gameServersContext.GetSingle(x => x.Name == check || x.ApiPort.ToString() == check);
    }

    [HttpPut]
    [Authorize]
    public async Task AddServer([FromBody] DomainGameServer gameServer)
    {
        gameServer.Order = gameServersContext.Get().Count();
        await gameServersContext.Add(gameServer);

        logger.LogAudit($"Server added '{gameServer}'");
        SendAnyUpdateIfNotCaller(true);
    }

    [HttpPatch]
    [Authorize]
    public async Task<bool> EditGameServer([FromBody] DomainGameServer gameServer)
    {
        var oldGameServer = gameServersContext.GetSingle(gameServer.Id);
        logger.LogAudit($"Game server '{gameServer.Name}' updated:{oldGameServer.Changes(gameServer)}");
        var environmentChanged = false;
        if (oldGameServer.Environment != gameServer.Environment)
        {
            environmentChanged = true;
            gameServer.Mods = gameServersService.GetEnvironmentMods(gameServer.Environment);
            gameServer.ServerMods = new List<GameServerMod>();
        }

        await gameServersContext.Update(
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
        var gameServer = gameServersContext.GetSingle(id);
        logger.LogAudit($"Game server deleted '{gameServer.Name}'");
        await gameServersContext.Delete(id);

        SendAnyUpdateIfNotCaller(true);
        return gameServersContext.Get();
    }

    [HttpPatch("order")]
    [Authorize]
    public async Task<IEnumerable<DomainGameServer>> UpdateOrder([FromBody] OrderUpdateRequest orderUpdate)
    {
        await gameServersService.UpdateGameServerOrder(orderUpdate);
        SendAnyUpdateIfNotCaller(true);
        return gameServersContext.Get();
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
                await gameServersService.UploadMissionFile(file);
                var missionPatchingResult = await gameServersService.PatchMissionFile(file.Name);
                missionPatchingResult.Reports = missionPatchingResult.Reports.OrderByDescending(x => x.Error).ToList();
                missionReports.Add(new MissionReportDataset { Mission = file.Name, Reports = missionPatchingResult.Reports });
                logger.LogAudit($"Uploaded mission '{file.Name}'");
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
            throw new BadRequestException(exception.Message); // TODO: Needs better error handling
        }

        var missions = gameServersService.GetMissionFiles();
        SendMissionsUpdateIfNotCaller(missions);
        return new MissionsDataset { Missions = missions, MissionReports = missionReports };
    }

    [HttpPost("launch/{id}")]
    [Authorize]
    public async Task<List<ValidationReport>> LaunchServer(string id, [FromBody] LaunchServerRequest launchServerRequest)
    {
        await Task.WhenAll(gameServersContext.Get().Select(gameServersService.GetGameServerStatus));
        var gameServer = gameServersContext.GetSingle(id);
        if (gameServer.Status.Running)
        {
            throw new BadRequestException("Server is already running. This shouldn't happen so please contact an admin");
        }

        if (gameServerHelpers.IsMainOpTime())
        {
            if (gameServer.ServerOption == GameServerOption.Singleton)
            {
                if (gameServersContext.Get(x => x.ServerOption != GameServerOption.Singleton).Any(x => x.Status.Started || x.Status.Running))
                {
                    throw new BadRequestException("Server must be launched on its own. Stop the other running servers first");
                }
            }

            if (gameServersContext.Get(x => x.ServerOption == GameServerOption.Singleton).Any(x => x.Status.Started || x.Status.Running))
            {
                throw new BadRequestException("Server cannot be launched whilst main server is running at this time");
            }
        }

        if (gameServersContext.Get(x => x.Port == gameServer.Port).Any(x => x.Status.Started || x.Status.Running))
        {
            throw new BadRequestException("Server cannot be launched while another server with the same port is running");
        }

        var patchingResult = await gameServersService.PatchMissionFile(launchServerRequest.MissionName);
        if (!patchingResult.Success)
        {
            patchingResult.Reports = patchingResult.Reports.OrderByDescending(x => x.Error).ToList();
            var error =
                $"{(patchingResult.Reports.Count > 0 ? "Failed to patch mission for the reasons detailed below" : "Failed to patch mission for an unknown reason")}.\n\nContact an admin for help";
            throw new MissionPatchingFailedException(error, new ValidationReportDataset { Reports = patchingResult.Reports });
        }

        gameServersService.WriteServerConfig(gameServer, patchingResult.PlayerCount, launchServerRequest.MissionName);
        gameServer.Status.Mission = launchServerRequest.MissionName;

        var currentUserId = httpContextService.GetUserId();
        gameServer.LaunchedBy = currentUserId;

        await gameServersService.LaunchGameServer(gameServer);

        logger.LogAudit($"Game server launched '{launchServerRequest.MissionName}' on '{gameServer.Name}'");
        SendServerUpdateIfNotCaller(gameServer.Id);
        return patchingResult.Reports;
    }

    [HttpPost("stop/{id}")]
    [Authorize]
    public async Task<GameServerDataset> StopServer(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        logger.LogAudit($"Game server stopped '{gameServer.Name}'");
        await gameServersService.GetGameServerStatus(gameServer);
        if (!gameServer.Status.Started && !gameServer.Status.Running)
        {
            throw new BadRequestException("Server is not running. This shouldn't happen so please contact an admin");
        }

        gameServer.LaunchedBy = null;

        SendServerUpdateIfNotCaller(gameServer.Id);
        await gameServersService.StopGameServer(gameServer);
        await gameServersService.GetGameServerStatus(gameServer);
        return new GameServerDataset { GameServer = gameServer, InstanceCount = gameServersService.GetGameInstanceCount() };
    }

    [HttpPost("kill/{id}")]
    [Authorize]
    public async Task<GameServerDataset> KillServer(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        logger.LogAudit($"Game server killed '{gameServer.Name}'");
        await gameServersService.GetGameServerStatus(gameServer);
        if (!gameServer.Status.Started && !gameServer.Status.Running)
        {
            throw new BadRequestException("Server is not running. This shouldn't happen so please contact an admin");
        }

        try
        {
            gameServer.LaunchedBy = null;
            await gameServersService.KillGameServer(gameServer);
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
            throw new BadRequestException("Failed to stop server. Contact an admin");
        }

        await gameServersService.GetGameServerStatus(gameServer);
        SendServerUpdateIfNotCaller(gameServer.Id);
        return new GameServerDataset { GameServer = gameServer, InstanceCount = gameServersService.GetGameInstanceCount() };
    }

    [HttpPost("killall")]
    [Authorize]
    public async Task KillAllArmaProcesses()
    {
        var gameServers = gameServersContext.Get().ToList();
        gameServers.ForEach(x => x.LaunchedBy = null);

        var killed = await gameServersService.KillAllArmaProcesses();

        logger.LogAudit($"Killed {killed} Arma instances");
        SendAnyUpdateIfNotCaller();
    }

    [HttpGet("{id}/mods")]
    [Authorize]
    public List<GameServerMod> GetAvailableMods(string id)
    {
        return gameServersService.GetAvailableMods(id);
    }

    [HttpPost("{id}/mods")]
    [Authorize]
    public async Task<List<GameServerMod>> SetGameServerMods(string id, [FromBody] DomainGameServer gameServer)
    {
        var oldGameServer = gameServersContext.GetSingle(id);
        await gameServersContext.Update(id, Builders<DomainGameServer>.Update.Unset(x => x.Mods).Unset(x => x.ServerMods));
        await gameServersContext.Update(id, Builders<DomainGameServer>.Update.Set(x => x.Mods, gameServer.Mods).Set(x => x.ServerMods, gameServer.ServerMods));
        logger.LogAudit($"Game server '{gameServer.Name}' updated:{oldGameServer.Changes(gameServer)}");
        return gameServersService.GetAvailableMods(id);
    }

    [HttpGet("{id}/mods/reset")]
    [Authorize]
    public GameServerModsDataset ResetGameServerMods(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        return new GameServerModsDataset
        {
            AvailableMods = gameServersService.GetAvailableMods(id),
            Mods = gameServersService.GetEnvironmentMods(gameServer.Environment),
            ServerMods = new List<GameServerMod>()
        };
    }

    [HttpGet("disabled")]
    [Authorize]
    public bool GetDisabledState()
    {
        return variablesService.GetVariable("SERVER_CONTROL_DISABLED").AsBool();
    }

    [HttpPost("disabled")]
    [Authorize]
    public async Task SetDisabledState([FromBody] SetDisabledStateRequest stateRequest)
    {
        await variablesContext.Update("SERVER_CONTROL_DISABLED", stateRequest.State);
        await serversHub.Clients.All.ReceiveDisabledState(stateRequest.State);
    }

    private void SendAnyUpdateIfNotCaller(bool skipRefresh = false)
    {
        if (!GetHubConnectionId(out var connectionId))
        {
            return;
        }

        _ = serversHub.Clients.All.ReceiveAnyUpdateIfNotCaller(connectionId, skipRefresh);
    }

    private void SendServerUpdateIfNotCaller(string serverId)
    {
        if (!GetHubConnectionId(out var connectionId))
        {
            return;
        }

        _ = serversHub.Clients.All.ReceiveServerUpdateIfNotCaller(connectionId, serverId);
    }

    private void SendMissionsUpdateIfNotCaller(List<MissionFile> missions)
    {
        if (!GetHubConnectionId(out var connectionId))
        {
            return;
        }

        _ = serversHub.Clients.All.ReceiveMissionsUpdateIfNotCaller(connectionId, missions);
    }

    private bool GetHubConnectionId(out StringValues connecctionId)
    {
        return HttpContext.Request.Headers.TryGetValue("Hub-Connection-Id", out connecctionId);
    }
}
