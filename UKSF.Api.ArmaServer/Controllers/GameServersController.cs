using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.Exceptions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Models.Parameters;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Request;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Nco, Permissions.Servers, Permissions.Command)]
public class GameServersController(
    IGameServersService gameServersService,
    IGameServerProcessManager processManager,
    IMissionsService missionsService,
    IRptLogService rptLogService,
    IGameServerHelpers gameServerHelpers,
    IUksfLogger logger,
    IHttpContextService httpContextService
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public GameServersUpdate GetGameServers()
    {
        var servers = gameServersService.GetServers();
        foreach (var server in servers)
        {
            server.LogSources = rptLogService.GetLogSources(server);
        }

        return new GameServersUpdate
        {
            Servers = servers,
            Missions = missionsService.GetActiveMissions(),
            InstanceCount = processManager.GetInstanceCount()
        };
    }

    [HttpPost("{check}")]
    [Authorize]
    public DomainGameServer CheckGameServers(string check, [FromBody] DomainGameServer gameServer = null)
    {
        return gameServersService.CheckServer(check, gameServer?.Id);
    }

    [HttpPut]
    [Authorize]
    public async Task AddServer([FromBody] DomainGameServer gameServer)
    {
        await gameServersService.AddServerAsync(gameServer);
        await processManager.PushAllServersUpdateAsync();
    }

    [HttpPatch]
    [Authorize]
    public async Task<bool> EditGameServer([FromBody] DomainGameServer gameServer)
    {
        var environmentChanged = await gameServersService.EditServerAsync(gameServer);
        await processManager.PushServerUpdateAsync(gameServersService.GetServer(gameServer.Id));
        return environmentChanged;
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task DeleteGameServer(string id)
    {
        await gameServersService.DeleteServerAsync(id);
        await processManager.PushAllServersUpdateAsync();
    }

    [HttpPatch("order")]
    [Authorize]
    public async Task UpdateOrder([FromBody] OrderUpdateRequest orderUpdate)
    {
        await gameServersService.UpdateGameServerOrder(orderUpdate);
        await processManager.PushAllServersUpdateAsync();
    }

    [HttpPost("{id}/launch")]
    [Authorize]
    public async Task<List<ValidationReport>> LaunchServer(string id, [FromBody] LaunchServerRequest launchServerRequest)
    {
        var gameServer = gameServersService.GetServer(id);
        if (gameServer.Status.Running || gameServer.Status.Launching)
        {
            throw new BadRequestException("Server is already running. This shouldn't happen so please contact an admin");
        }

        var allServers = gameServersService.GetServers();

        if (gameServerHelpers.IsMainOpTime())
        {
            if (gameServer.ServerOption == GameServerOption.Singleton)
            {
                if (allServers.Where(x => x.ServerOption != GameServerOption.Singleton).Any(x => x.Status.Launching || x.Status.Running))
                {
                    throw new BadRequestException("Server must be launched on its own. Stop the other running servers first");
                }
            }

            if (allServers.Where(x => x.ServerOption == GameServerOption.Singleton).Any(x => x.Status.Launching || x.Status.Running))
            {
                throw new BadRequestException("Server cannot be launched whilst main server is running at this time");
            }
        }

        if (allServers.Where(x => x.Port == gameServer.Port).Any(x => x.Status.Launching || x.Status.Running))
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

        var currentUserId = httpContextService.GetUserId();
        await processManager.LaunchServerAsync(gameServer, launchServerRequest.MissionName, currentUserId, patchingResult.PlayerCount);

        logger.LogAudit($"Game server launched '{launchServerRequest.MissionName}' on '{gameServer.Name}'");

        return patchingResult.Reports;
    }

    [HttpPost("{id}/stop")]
    [Authorize]
    public async Task StopServer(string id)
    {
        var gameServer = gameServersService.GetServer(id);
        if (!gameServer.Status.Launching && !gameServer.Status.Running)
        {
            throw new BadRequestException("Server is not running. This shouldn't happen so please contact an admin");
        }

        logger.LogAudit($"Game server stopped '{gameServer.Name}'");
        await processManager.StopServerAsync(gameServer);
    }

    [HttpPost("{id}/kill")]
    [Authorize]
    public async Task KillServer(string id)
    {
        var gameServer = gameServersService.GetServer(id);
        if (!gameServer.Status.Launching && !gameServer.Status.Running && !gameServer.Status.Stopping)
        {
            throw new BadRequestException("Server is not running. This shouldn't happen so please contact an admin");
        }

        logger.LogAudit($"Game server killed '{gameServer.Name}'");
        try
        {
            await processManager.KillServerAsync(gameServer);
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
            throw new BadRequestException("Failed to stop server. Contact an admin");
        }
    }

    [HttpPost("killall")]
    [Authorize]
    public async Task KillAllArmaProcesses()
    {
        var killed = await processManager.KillAllAsync();

        logger.LogAudit($"Killed {killed} Arma instances");
        await processManager.PushAllServersUpdateAsync();
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
        await gameServersService.SetServerModsAsync(id, gameServer);
        return gameServersService.GetAvailableMods(id);
    }

    [HttpGet("{id}/mods/reset")]
    [Authorize]
    public GameServerModsDataset ResetGameServerMods(string id)
    {
        return gameServersService.ResetServerMods(id);
    }

    [HttpGet("disabled")]
    [Authorize]
    public bool GetDisabledState()
    {
        return gameServersService.GetDisabledState();
    }

    [HttpPost("disabled")]
    [Authorize]
    public async Task SetDisabledState([FromBody] SetDisabledStateRequest stateRequest)
    {
        await gameServersService.SetDisabledStateAsync(stateRequest.State);
    }

    [HttpGet("{id}/log/download")]
    [Authorize]
    public IActionResult DownloadLog(string id, [FromQuery] string source)
    {
        var server = gameServersService.GetServer(id);
        var filePath = rptLogService.GetLatestRptFilePath(server, source);
        if (filePath == null)
        {
            return NotFound("No log file found");
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(stream, "text/plain", Path.GetFileName(filePath));
    }
}
