using System.Text.RegularExpressions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Queries;

public interface IGetCurrentServerInfrastructureQuery
{
    Task<ServerInfrastructureCurrent> ExecuteAsync();
}

public class GetCurrentServerInfrastructureQuery : IGetCurrentServerInfrastructureQuery
{
    private readonly IVariablesService _variablesService;

    public GetCurrentServerInfrastructureQuery(IVariablesService variablesService)
    {
        _variablesService = variablesService;
    }

    public async Task<ServerInfrastructureCurrent> ExecuteAsync()
    {
        var steamPath = _variablesService.GetVariable("SERVER_PATH_STEAM").AsString();
        var manifestPath = Path.Combine(steamPath, "steamapps", "appmanifest_233780.acf");
        var manifest = await File.ReadAllTextAsync(manifestPath);
        if (manifest.Length == 0)
        {
            return new ServerInfrastructureCurrent { CurrentBuild = "0" };
        }

        var buildIdMatch = new Regex(@"""buildid""\s+""(\d*)""").Match(manifest);
        if (!buildIdMatch.Success)
        {
            return new ServerInfrastructureCurrent { CurrentBuild = "0" };
        }

        var lastUpdatedMatch = new Regex(@"""LastUpdated""\s+""(\d*)""").Match(manifest);
        if (!lastUpdatedMatch.Success)
        {
            return new ServerInfrastructureCurrent { CurrentBuild = "0" };
        }

        var buildId = buildIdMatch.Groups[1].Value;
        var lastUpdatedUnix = long.Parse(lastUpdatedMatch.Groups[1].Value);
        var lastUpdated = DateTimeOffset.FromUnixTimeSeconds(lastUpdatedUnix);

        return new ServerInfrastructureCurrent { CurrentBuild = buildId, CurrentUpdated = lastUpdated.UtcDateTime };
    }
}
