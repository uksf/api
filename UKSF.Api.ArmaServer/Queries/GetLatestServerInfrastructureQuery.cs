using Gameloop.Vdf;
using Gameloop.Vdf.JsonConverter;
using UKSF.Api.ArmaServer.Exceptions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;

namespace UKSF.Api.ArmaServer.Queries;

public interface IGetLatestServerInfrastructureQuery
{
    Task<ServerInfrastructureLatest> ExecuteAsync();
}

public class GetLatestServerInfrastructureQuery : IGetLatestServerInfrastructureQuery
{
    private readonly ISteamCmdService _steamCmdService;

    public GetLatestServerInfrastructureQuery(ISteamCmdService steamCmdService)
    {
        _steamCmdService = steamCmdService;
    }

    public async Task<ServerInfrastructureLatest> ExecuteAsync()
    {
        var output = await _steamCmdService.GetServerInfo();
        if (output.Contains("No app info for AppID 233780"))
        {
            throw new ServerInfrastructureException("No info found from Steam", 404);
        }

        output = output[output.IndexOf(@"""233780""", StringComparison.Ordinal)..];
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
