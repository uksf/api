using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.ArmaServer.Commands
{
    public interface IUpdateServerInfrastructureCommand
    {
        Task<string> ExecuteAsync();
    }

    public class UpdateServerInfrastructureCommand : IUpdateServerInfrastructureCommand
    {
        private readonly ISteamCmdService _steamCmdService;
        private readonly IVariablesContext _variablesContext;
        private readonly IVariablesService _variablesService;
        private readonly IGameServersService _gameServersService;
        private readonly ILogger _logger;

        public UpdateServerInfrastructureCommand(
            ISteamCmdService steamCmdService,
            IVariablesContext variablesContext,
            IVariablesService variablesService,
            IGameServersService gameServersService,
            ILogger logger
        )
        {
            _steamCmdService = steamCmdService;
            _variablesContext = variablesContext;
            _variablesService = variablesService;
            _gameServersService = gameServersService;
            _logger = logger;
        }

        public async Task<string> ExecuteAsync()
        {
            if (_variablesService.GetVariable("SERVER_INFRA_UPDATING").AsBool())
            {
                throw new BadRequestException("Server infrastructure is already updating");
            }

            var instances = _gameServersService.GetGameInstanceCount();
            if (instances != 0)
            {
                throw new BadRequestException("There are servers running, cannot update infrastructure at this time");
            }

            await _variablesContext.Update("SERVER_INFRA_UPDATING", true);
            _logger.LogInfo("Server infrastructure update starting");
            try
            {
                var result = await _steamCmdService.UpdateServer();
                CopyFiles();

                return result;
            }
            finally
            {
                await _variablesContext.Update("SERVER_INFRA_UPDATING", false);
                _logger.LogInfo("Server infrastructure update finished");
            }
        }

        private void CopyFiles()
        {
            var serverPath = _variablesService.GetVariable("SERVER_INFRA_PATH").AsString();
            var allowedExtensions = new Regex("exe|dll", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var serverFiles = new DirectoryInfo(serverPath).EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                                                           .Where(x => allowedExtensions.IsMatch(x.Extension))
                                                           .ToList();

            var releasePath = _variablesService.GetVariable("SERVER_PATH_RELEASE").AsString();
            var rcPath = _variablesService.GetVariable("SERVER_PATH_RC").AsString();
            var devPath = _variablesService.GetVariable("SERVER_PATH_DEV").AsString();
            foreach (var serverFile in serverFiles)
            {
                var releaseFile = Path.Join(releasePath, serverFile.Name);
                var rcFile = Path.Join(rcPath, serverFile.Name);
                var devFile = Path.Join(devPath, serverFile.Name);
                serverFile.CopyTo(releaseFile, true);
                serverFile.CopyTo(rcFile, true);
                serverFile.CopyTo(devFile, true);
            }
        }
    }
}
