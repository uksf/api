using Gameloop.Vdf;
using Gameloop.Vdf.JsonConverter;
using Microsoft.Extensions.Options;
using UKSF.Api.ArmaServer.Exceptions;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;

namespace UKSF.Api.ArmaServer.Queries;

public interface IGetLatestServerInfrastructureQuery
{
    Task<ServerInfrastructureLatest> ExecuteAsync(int retryDelay = 1);
}

public class GetLatestServerInfrastructureQuery : IGetLatestServerInfrastructureQuery
{
    private readonly AppSettings _appSettings;
    private readonly IUksfLogger _logger;
    private readonly ISteamCmdService _steamCmdService;

    public GetLatestServerInfrastructureQuery(ISteamCmdService steamCmdService, IOptions<AppSettings> options, IUksfLogger logger)
    {
        _appSettings = options.Value;
        _steamCmdService = steamCmdService;
        _logger = logger;
    }

    public async Task<ServerInfrastructureLatest> ExecuteAsync(int retryDelay = 1)
    {
        string output;
        var tries = 0;

        do
        {
            output = await _steamCmdService.GetServerInfo();
            if (output.Contains("change number"))
            {
                break;
            }

            tries++;
            await Task.Delay(TimeSpan.FromSeconds(retryDelay));
        }
        while (tries < 10);

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

        try
        {
            output = output[appInfoIndex..];
            output = string.Join('}', output.Split("}")[..^1]);
            output += "}";
            output = output.Replace("OK", ""); // SteamCMD keeps being shit

            var latestJson = VdfConvert.Deserialize(output).ToJson();
            var buildInfo = latestJson.Value.SelectToken("depots.branches.creatordlc");
            if (buildInfo == null)
            {
                throw new ServerInfrastructureException("No build info found in Steam data", 404);
            }

            var buildId = buildInfo["buildid"]?.ToString();
            var updatedUnix = long.Parse(buildInfo["timeupdated"]?.ToString()!);
            var updated = DateTimeOffset.FromUnixTimeSeconds(updatedUnix);

            return new ServerInfrastructureLatest { LatestBuild = buildId, LatestUpdate = updated.UtcDateTime };
        }
        catch (Exception)
        {
            await DumpOutputToFile(output);
            throw;
        }
    }

    private async Task DumpOutputToFile(string output)
    {
        try
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _appSettings.LogsPath);
            await File.WriteAllTextAsync(Path.Combine(appData, $"{DateTime.UtcNow:yyMMddHHmmss}_steamcmd_output"), output);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception);
        }
    }
}
