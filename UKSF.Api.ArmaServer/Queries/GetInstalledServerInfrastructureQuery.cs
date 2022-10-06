using System.Diagnostics;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.ArmaServer.Queries;

public interface IGetInstalledServerInfrastructureQuery
{
    Task<ServerInfrastructureInstalled> ExecuteAsync();
}

public class GetInstalledServerInfrastructureQuery : IGetInstalledServerInfrastructureQuery
{
    private readonly IVariablesService _variablesService;

    public GetInstalledServerInfrastructureQuery(IVariablesService variablesService)
    {
        _variablesService = variablesService;
    }

    public Task<ServerInfrastructureInstalled> ExecuteAsync()
    {
        var releasePath = _variablesService.GetVariable("SERVER_PATH_RELEASE").AsString();
        var exePath = Path.Combine(releasePath, "arma3server_x64.exe");

        if (!File.Exists(exePath))
        {
            return Task.FromResult<ServerInfrastructureInstalled>(new() { InstalledVersion = "0" });
        }

        var fileVersion = FileVersionInfo.GetVersionInfo(exePath).ProductVersion;
        if (fileVersion == null)
        {
            return Task.FromResult<ServerInfrastructureInstalled>(new() { InstalledVersion = "0" });
        }

        var gameVersion = string.Join('.', fileVersion.Split('.').Take(2));

        return Task.FromResult<ServerInfrastructureInstalled>(new() { InstalledVersion = gameVersion });
    }
}
