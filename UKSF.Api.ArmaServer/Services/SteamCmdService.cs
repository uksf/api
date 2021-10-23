using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Configuration;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;

namespace UKSF.Api.ArmaServer.Services
{
    public interface ISteamCmdService
    {
        Task<string> GetServerInfo();
        Task<string> UpdateServer();
    }

    public class SteamCmdService : ISteamCmdService
    {
        private readonly IConfiguration _configuration;
        private readonly IVariablesService _variablesService;

        public SteamCmdService(IVariablesService variablesService, IConfiguration configuration)
        {
            _variablesService = variablesService;
            _configuration = configuration;
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
            var username = _configuration.GetSection("SteamCmd")["username"];
            var password = _configuration.GetSection("SteamCmd")["password"];

            var result = await Cli.Wrap(cmdPath)
                                  .WithWorkingDirectory(steamPath)
                                  .WithArguments($"+login {username} {password} +\"app_update 233780 -beta creatordlc\" validate +quit")
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
}
