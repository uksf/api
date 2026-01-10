using System.Diagnostics;
using UKSF.Api.ArmaServer.Models;
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
        var exePath = Path.Combine(releasePath, "arma3server_x64.exe");

        if (!File.Exists(exePath))
        {
            return Task.FromResult(new ServerInfrastructureInstalled { InstalledVersion = "0" });
        }

        var fileVersion = FileVersionInfo.GetVersionInfo(exePath).ProductVersion;
        if (fileVersion == null)
        {
            return Task.FromResult(new ServerInfrastructureInstalled { InstalledVersion = "0" });
        }

        var gameVersion = string.Join('.', fileVersion.Split('.').Take(2));

        return Task.FromResult(new ServerInfrastructureInstalled { InstalledVersion = gameVersion });
    }
}
