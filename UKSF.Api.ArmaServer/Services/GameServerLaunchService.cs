using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaServer.Exceptions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServerLaunchService
{
    Task<List<ValidationReport>> LaunchAsync(string serverId, string missionName, string launchedBy);
}

public class GameServerLaunchService(
    IGameServersService gameServersService,
    IMissionsService missionsService,
    IGameServerProcessManager processManager,
    IGameServerHelpers gameServerHelpers
) : IGameServerLaunchService
{
    public async Task<List<ValidationReport>> LaunchAsync(string serverId, string missionName, string launchedBy)
    {
        var gameServer = gameServersService.GetServer(serverId);
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

        var patchingResult = await missionsService.PatchMissionFile(missionName);
        if (!patchingResult.Success)
        {
            patchingResult.Reports = patchingResult.Reports.OrderByDescending(x => x.Error).ToList();
            var error =
                $"{(patchingResult.Reports.Count > 0 ? "Failed to patch mission for the reasons detailed below" : "Failed to patch mission for an unknown reason")}.\n\nContact an admin for help";
            throw new MissionPatchingFailedException(error, new ValidationReportDataset { Reports = patchingResult.Reports });
        }

        await processManager.LaunchServerAsync(gameServer, missionName, launchedBy, patchingResult.PlayerCount);
        return patchingResult.Reports;
    }
}
