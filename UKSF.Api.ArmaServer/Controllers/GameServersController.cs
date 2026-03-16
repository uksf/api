using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    IMissionsService missionsService,
    IHubContext<ServersHub, IServersClient> serversHub,
    IVariablesService variablesService,
    IGameServerHelpers gameServerHelpers,
    IRptLogService rptLogService,
    IUksfLogger logger,
    IHttpContextService httpContextService,
    IGameServerProcessMonitor processMonitor
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public GameServersUpdate GetGameServers()
    {
        var servers = gameServersContext.Get().ToList();
        foreach (var server in servers)
        {
            server.LogSources = rptLogService.GetLogSources(server);
        }

        return new GameServersUpdate
        {
            Servers = servers,
            Missions = missionsService.GetActiveMissions(),
            InstanceCount = gameServersService.GetGameInstanceCount()
        };
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
        await PushServersUpdate();
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

        gameServer = gameServersContext.GetSingle(gameServer.Id);
        await PushServerUpdate(gameServer);
        return environmentChanged;
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task DeleteGameServer(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        logger.LogAudit($"Game server deleted '{gameServer.Name}'");
        await gameServersContext.Delete(id);

        await PushServersUpdate();
    }

    [HttpPatch("order")]
    [Authorize]
    public async Task UpdateOrder([FromBody] OrderUpdateRequest orderUpdate)
    {
        await gameServersService.UpdateGameServerOrder(orderUpdate);
        await PushServersUpdate();
    }

    [HttpPost("launch/{id}")]
    [Authorize]
    public async Task<List<ValidationReport>> LaunchServer(string id, [FromBody] LaunchServerRequest launchServerRequest)
    {
        var gameServer = gameServersContext.GetSingle(id);
        if (gameServer.Status.Running || gameServer.Status.Launching)
        {
            throw new BadRequestException("Server is already running. This shouldn't happen so please contact an admin");
        }

        if (gameServerHelpers.IsMainOpTime())
        {
            if (gameServer.ServerOption == GameServerOption.Singleton)
            {
                if (gameServersContext.Get(x => x.ServerOption != GameServerOption.Singleton).Any(x => x.Status.Launching || x.Status.Running))
                {
                    throw new BadRequestException("Server must be launched on its own. Stop the other running servers first");
                }
            }

            if (gameServersContext.Get(x => x.ServerOption == GameServerOption.Singleton).Any(x => x.Status.Launching || x.Status.Running))
            {
                throw new BadRequestException("Server cannot be launched whilst main server is running at this time");
            }
        }

        if (gameServersContext.Get(x => x.Port == gameServer.Port).Any(x => x.Status.Launching || x.Status.Running))
        {
            throw new BadRequestException("Server cannot be launched while another server with the same port is running");
        }

        var patchingResult = await missionsService.PatchMissionFile(launchServerRequest.MissionName);
        if (!patchingResult.Success)
        {
            patchingResult.Reports = patchingResult.Reports.OrderByDescending(x => x.Error).ToList();
            var error =
                $"{(patchingResult.Reports.Count > 0 ? "Failed to patch mission for the reasons detailed below" : "Failed to patch mission for an unknown reason")}.\n\nContact an admin for help";
            throw new MissionPatchingFailedException(error, new ValidationReportDataset { Reports = patchingResult.Reports });
        }

        gameServersService.WriteServerConfig(gameServer, patchingResult.PlayerCount, launchServerRequest.MissionName);

        var currentUserId = httpContextService.GetUserId();
        await gameServersService.LaunchGameServer(gameServer, launchServerRequest.MissionName, currentUserId);

        logger.LogAudit($"Game server launched '{launchServerRequest.MissionName}' on '{gameServer.Name}'");

        await PushServerUpdate(gameServer);
        processMonitor.EnsureRunning();

        return patchingResult.Reports;
    }

    [HttpPost("stop/{id}")]
    [Authorize]
    public async Task StopServer(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        if (!gameServer.Status.Launching && !gameServer.Status.Running)
        {
            throw new BadRequestException("Server is not running. This shouldn't happen so please contact an admin");
        }

        gameServer.Status.Stopping = true;
        gameServer.Status.StoppingInitiatedAt = DateTime.UtcNow;
        await gameServersContext.Replace(gameServer);

        logger.LogAudit($"Game server stopped '{gameServer.Name}'");
        try
        {
            await gameServersService.StopGameServer(gameServer);
        }
        finally
        {
            await PushServerUpdate(gameServer);
            processMonitor.EnsureRunning();
        }
    }

    [HttpPost("kill/{id}")]
    [Authorize]
    public async Task KillServer(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        if (!gameServer.Status.Launching && !gameServer.Status.Running && !gameServer.Status.Stopping)
        {
            throw new BadRequestException("Server is not running. This shouldn't happen so please contact an admin");
        }

        logger.LogAudit($"Game server killed '{gameServer.Name}'");
        try
        {
            await gameServersService.KillGameServer(gameServer);
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
            throw new BadRequestException("Failed to stop server. Contact an admin");
        }

        await PushServerUpdate(gameServer);
        processMonitor.EnsureRunning();
    }

    [HttpPost("killall")]
    [Authorize]
    public async Task KillAllArmaProcesses()
    {
        var killed = await gameServersService.KillAllArmaProcesses();

        logger.LogAudit($"Killed {killed} Arma instances");
        await PushServersUpdate();
        processMonitor.EnsureRunning();
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

    [HttpGet("{id}/log/download")]
    [Authorize]
    public IActionResult DownloadLog(string id, [FromQuery] string source)
    {
        var server = gameServersContext.GetSingle(id);
        var filePath = rptLogService.GetLatestRptFilePath(server, source);
        if (filePath == null)
        {
            return NotFound("No log file found");
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(stream, "text/plain", Path.GetFileName(filePath));
    }

    private async Task PushServersUpdate()
    {
        var servers = gameServersContext.Get().ToList();
        foreach (var server in servers)
        {
            server.LogSources = rptLogService.GetLogSources(server);
        }

        var update = new GameServersUpdate
        {
            Servers = servers,
            Missions = missionsService.GetActiveMissions(),
            InstanceCount = gameServersService.GetGameInstanceCount()
        };
        await serversHub.Clients.All.ReceiveServersUpdate(update);
    }

    private async Task PushServerUpdate(DomainGameServer server)
    {
        server.LogSources = rptLogService.GetLogSources(server);
        var update = new GameServerUpdate { Server = server, InstanceCount = gameServersService.GetGameInstanceCount() };
        await serversHub.Clients.All.ReceiveServerUpdate(update);
    }
}
