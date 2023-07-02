using Gameloop.Vdf;
using Gameloop.Vdf.JsonConverter;
using UKSF.Api.ArmaServer.Exceptions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Queries;

public interface IGetLatestServerInfrastructureQuery
{
    Task<ServerInfrastructureLatest> ExecuteAsync();
}

public class GetLatestServerInfrastructureQuery : IGetLatestServerInfrastructureQuery
{
    private readonly ISteamCmdService _steamCmdService;
    private readonly IUksfLogger _logger;

    public GetLatestServerInfrastructureQuery(ISteamCmdService steamCmdService, IUksfLogger logger)
    {
        _steamCmdService = steamCmdService;
        _logger = logger;
    }

    public async Task<ServerInfrastructureLatest> ExecuteAsync()
    {
        var output = string.Empty;
        var tries = 0;

        while (!output.Contains("change number") && tries < 10)
        {
            output = await _steamCmdService.GetServerInfo();
            tries++;
        }

        if (tries >= 10 || !output.Contains("change number") || output.Contains("No app info for AppID 233780"))
        {
            throw new ServerInfrastructureException("No info found from Steam", 404);
        }

        var appInfoIndex = output.IndexOf(@"""233780""", StringComparison.Ordinal);
        if (appInfoIndex < 0)
        {
            _logger.LogInfo(output);
            throw new ServerInfrastructureException("Unable to parse app info data from Steam", 404);
        }

        output = output[appInfoIndex..];
        output = string.Join('}', output.Split("}")[..^1]);
        output += "}";

        var latestJson = VdfConvert.Deserialize(output).ToJson();
        var buildInfo = latestJson.Value.SelectToken("depots.branches.creatordlc");
        if (buildInfo == null)
        {
            throw new ServerInfrastructureException("No build info found in Steam data", 404);
        }

        var buildId = buildInfo["buildid"]?.ToString();
        var updatedUnix = long.Parse(buildInfo["timeupdated"]?.ToString()!);
        var updated = DateTimeOffset.FromUnixTimeSeconds(updatedUnix);

        return new() { LatestBuild = buildId, LatestUpdate = updated.UtcDateTime };
    }
}
