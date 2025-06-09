using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Commands;

public interface IUpdateServerInfrastructureCommand
{
    Task<string> ExecuteAsync();
}

public class UpdateServerInfrastructureCommand(
    ISteamCmdService steamCmdService,
    IVariablesContext variablesContext,
    IVariablesService variablesService,
    IGameServersService gameServersService,
    IHubContext<ServersHub, IServersClient> serversHub,
    IUksfLogger logger
) : IUpdateServerInfrastructureCommand
{
    public async Task<string> ExecuteAsync()
    {
        if (variablesService.GetVariable("SERVER_INFRA_UPDATING").AsBool())
        {
            throw new BadRequestException("Server infrastructure is already updating");
        }

        var instances = gameServersService.GetGameInstanceCount();
        if (instances != 0)
        {
            throw new BadRequestException("There are servers running, cannot update infrastructure at this time");
        }

        await variablesContext.Update("SERVER_INFRA_UPDATING", true);
        await variablesContext.Update("SERVER_CONTROL_DISABLED", true);
        await serversHub.Clients.All.ReceiveDisabledState(true);
        logger.LogInfo("Server infrastructure update starting");

        try
        {
            var result = await steamCmdService.UpdateServer();
            CopyFiles();

            return result;
        }
        finally
        {
            await variablesContext.Update("SERVER_INFRA_UPDATING", false);
            await variablesContext.Update("SERVER_CONTROL_DISABLED", false);
            await serversHub.Clients.All.ReceiveDisabledState(false);
            logger.LogInfo("Server infrastructure update finished");
        }
    }

    private void CopyFiles()
    {
        var serverPath = variablesService.GetVariable("SERVER_INFRA_PATH").AsString();
        var allowedExtensions = new Regex("exe|dll", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var serverFiles = new DirectoryInfo(serverPath).EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                                                       .Where(x => allowedExtensions.IsMatch(x.Extension))
                                                       .ToList();

        var releasePath = variablesService.GetVariable("SERVER_PATH_RELEASE").AsString();
        var rcPath = variablesService.GetVariable("SERVER_PATH_RC").AsString();
        var devPath = variablesService.GetVariable("SERVER_PATH_DEV").AsString();
        foreach (var serverFile in serverFiles)
        {
            var releaseFile = Path.Join(releasePath, serverFile.Name);
            var rcFile = Path.Join(rcPath, serverFile.Name);
            var devFile = Path.Join(devPath, serverFile.Name);
            serverFile.CopyTo(releaseFile, true);
            serverFile.CopyTo(rcFile, true);
            serverFile.CopyTo(devFile, true);
        }
    }
}
