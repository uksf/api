using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Services;

public interface ISteamCmdService
{
    Task<string> GetServerInfo();
    Task<string> UpdateServer();
    Task<string> DownloadWorkshopMod(string workshopModId);
    Task<string> RefreshLogin();
}

public class SteamCmdService : ISteamCmdService
{
    private readonly string _password;
    private readonly string _username;
    private readonly IVariablesService _variablesService;
    private readonly ISteamGuardCodeService _steamGuardCodeService;

    public SteamCmdService(IVariablesService variablesService, IOptions<AppSettings> options, ISteamGuardCodeService steamGuardCodeService)
    {
        _variablesService = variablesService;
        _steamGuardCodeService = steamGuardCodeService;

        var appSettings = options.Value;
        _username = appSettings.Secrets.SteamCmd.Username;
        _password = appSettings.Secrets.SteamCmd.Password;
    }

    public async Task<string> GetServerInfo()
    {
        return await ExecuteSteamCmd("+login anonymous +app_info_update 1 +app_info_print 233780 +logoff +quit");
    }

    public async Task<string> UpdateServer()
    {
        return await ExecuteSteamCmd($"{BuildLogin()} +\"app_update 233780 -beta creatordlc\" validate +quit");
    }

    public async Task<string> DownloadWorkshopMod(string workshopModId)
    {
        var output = await ExecuteSteamCmd($"{BuildLogin()} +workshop_download_item 107410 {workshopModId} +quit");

        if (output.Contains("failed"))
        {
            throw new Exception(output);
        }

        return output;
    }

    public async Task<string> RefreshLogin()
    {
        return await ExecuteSteamCmd($"{BuildLogin()} +quit");
    }

    private string BuildLogin()
    {
        var guardCode = _steamGuardCodeService.GenerateCode();
        return guardCode is null ? $"+login {_username} {_password}" : $"+login {_username} {_password} {guardCode}";
    }

    private async Task<string> ExecuteSteamCmd(string arguments)
    {
        var steamPath = _variablesService.GetVariable("SERVER_PATH_STEAM").AsString();
        var cmdPath = Path.Combine(steamPath, "steamcmd.exe");

        var result = await Cli.Wrap(cmdPath)
                              .WithWorkingDirectory(steamPath)
                              .WithArguments(arguments)
                              .WithValidation(CommandResultValidation.None)
                              .ExecuteBufferedAsync();

        return result.StandardOutput;
    }
}
