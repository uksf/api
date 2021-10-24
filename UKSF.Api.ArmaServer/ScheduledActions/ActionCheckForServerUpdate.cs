using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaServer.Commands;
using UKSF.Api.ArmaServer.Queries;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.ArmaServer.ScheduledActions
{
    public interface IActionCheckForServerUpdate : ISelfCreatingScheduledAction { }

    public class ActionCheckForServerUpdate : IActionCheckForServerUpdate
    {
        private const string ACTION_NAME = nameof(ActionCheckForServerUpdate);

        private readonly IHostEnvironment _currentEnvironment;
        private readonly IClock _clock;
        private readonly IVariablesService _variablesService;
        private readonly IUpdateServerInfrastructureCommand _updateServerInfrastructureCommand;
        private readonly IGetLatestServerInfrastructureQuery _getLatestServerInfrastructureQuery;
        private readonly IGetCurrentServerInfrastructureQuery _getCurrentServerInfrastructureQuery;
        private readonly IGetInstalledServerInfrastructureQuery _getInstalledServerInfrastructureQuery;
        private readonly ILogger _logger;
        private readonly ISchedulerContext _schedulerContext;
        private readonly ISchedulerService _schedulerService;

        public ActionCheckForServerUpdate(
            ISchedulerService schedulerService,
            ISchedulerContext schedulerContext,
            IHostEnvironment currentEnvironment,
            IClock clock,
            IVariablesService variablesService,
            IUpdateServerInfrastructureCommand updateServerInfrastructureCommand,
            IGetLatestServerInfrastructureQuery getLatestServerInfrastructureQuery,
            IGetCurrentServerInfrastructureQuery getCurrentServerInfrastructureQuery,
            IGetInstalledServerInfrastructureQuery getInstalledServerInfrastructureQuery,
            ILogger logger
        )
        {
            _schedulerService = schedulerService;
            _schedulerContext = schedulerContext;
            _currentEnvironment = currentEnvironment;
            _clock = clock;
            _variablesService = variablesService;
            _updateServerInfrastructureCommand = updateServerInfrastructureCommand;
            _getLatestServerInfrastructureQuery = getLatestServerInfrastructureQuery;
            _getCurrentServerInfrastructureQuery = getCurrentServerInfrastructureQuery;
            _getInstalledServerInfrastructureQuery = getInstalledServerInfrastructureQuery;
            _logger = logger;
        }

        public string Name => ACTION_NAME;

        public async Task Run(params object[] parameters)
        {
            if (!_variablesService.GetFeatureState("FEATURE_AUTO_INFRA_UPDATE"))
            {
                return;
            }

            var latestInfo = await _getLatestServerInfrastructureQuery.ExecuteAsync();
            var currentInfo = await _getCurrentServerInfrastructureQuery.ExecuteAsync();
            var installedInfo = await _getInstalledServerInfrastructureQuery.ExecuteAsync();

            if (latestInfo.LatestBuild != currentInfo.CurrentBuild ||
                latestInfo.LatestUpdate > currentInfo.CurrentUpdated ||
                installedInfo.InstalledVersion == "0")
            {
                _logger.LogInfo("Server infrastructure update required");
                await _updateServerInfrastructureCommand.ExecuteAsync();

                var after = await _getInstalledServerInfrastructureQuery.ExecuteAsync();
                _logger.LogInfo($"Server infrastructure updated from version {installedInfo.InstalledVersion} to {after.InstalledVersion}");
            }
        }

        public async Task CreateSelf()
        {
            if (_currentEnvironment.IsDevelopment())
            {
                return;
            }

            if (_schedulerContext.GetSingle(x => x.Action == ACTION_NAME) == null)
            {
                await _schedulerService.CreateScheduledJob(_clock.UtcNow().Date.AddHours(18).AddDays(1), TimeSpan.FromDays(1), ACTION_NAME);
            }
        }

        public Task Reset()
        {
            return Task.CompletedTask;
        }
    }
}
