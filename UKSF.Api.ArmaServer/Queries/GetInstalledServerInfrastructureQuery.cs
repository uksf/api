using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Queries;

public interface IGetInstalledServerInfrastructureQuery
{
    Task<ServerInfrastructureInstalled> ExecuteAsync();
}

public class GetInstalledServerInfrastructureQuery(IVariablesService variablesService) : IGetInstalledServerInfrastructureQuery
{
    public Task<ServerInfrastructureInstalled> ExecuteAsync()
    {
        var releasePath = variablesService.GetVariable("SERVER_PATH_RELEASE").AsString();
        var exePath = ArmaServerExecutable.PathIn(releasePath);

        var gameVersion = File.Exists(exePath) ? ArmaServerExecutable.ReadGameVersion(exePath) : null;
        return Task.FromResult(new ServerInfrastructureInstalled { InstalledVersion = gameVersion ?? "0" });
    }
}
