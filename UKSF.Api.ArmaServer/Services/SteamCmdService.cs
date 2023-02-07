using System.Diagnostics;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface ISteamCmdService
{
    Task<string> GetServerInfo();
    Task<string> UpdateServer();
}

public class SteamCmdService : ISteamCmdService
{
    private readonly string _password;
    private readonly string _username;
    private readonly IVariablesService _variablesService;

    public SteamCmdService(IVariablesService variablesService, IOptions<AppSettings> options)
    {
        _variablesService = variablesService;
        var appSettings = options.Value;
        _username = appSettings.Secrets.SteamCmd.Username;
        _password = appSettings.Secrets.SteamCmd.Password;
    }

    public async Task<string> GetServerInfo()
    {
        var process = ExecuteSteamCmdCommand("+login anonymous +app_info_update 1 +app_info_print 233780 +quit");

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output;
    }

    public async Task<string> UpdateServer()
    {
        var steamPath = _variablesService.GetVariable("SERVER_PATH_STEAM").AsString();
        var cmdPath = Path.Combine(steamPath, "steamcmd.exe");

        var result = await Cli.Wrap(cmdPath)
                              .WithWorkingDirectory(steamPath)
                              .WithArguments($"+login {_username} {_password} +\"app_update 233780 -beta creatordlc\" validate +quit")
                              .ExecuteBufferedAsync();

        return result.StandardOutput;
    }

    private Process ExecuteSteamCmdCommand(string command)
    {
        var steamPath = _variablesService.GetVariable("SERVER_PATH_STEAM").AsString();
        var cmdPath = Path.Combine(steamPath, "steamcmd.exe");

        return new()
        {
            StartInfo =
            {
                FileName = cmdPath,
                WorkingDirectory = steamPath,
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
    }
}
