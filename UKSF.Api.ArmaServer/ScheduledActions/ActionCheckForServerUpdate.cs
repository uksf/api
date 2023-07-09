using UKSF.Api.ArmaServer.Commands;
using UKSF.Api.ArmaServer.Queries;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.ScheduledActions;

public interface IActionCheckForServerUpdate : ISelfCreatingScheduledAction { }

public class ActionCheckForServerUpdate : SelfCreatingScheduledAction, IActionCheckForServerUpdate
{
    private const string ActionName = nameof(ActionCheckForServerUpdate);
    private readonly IClock _clock;

    private readonly IGetCurrentServerInfrastructureQuery _getCurrentServerInfrastructureQuery;
    private readonly IGetInstalledServerInfrastructureQuery _getInstalledServerInfrastructureQuery;
    private readonly IGetLatestServerInfrastructureQuery _getLatestServerInfrastructureQuery;
    private readonly IUksfLogger _logger;
    private readonly IUpdateServerInfrastructureCommand _updateServerInfrastructureCommand;
    private readonly IVariablesService _variablesService;

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
        IUksfLogger logger
    ) : base(schedulerService, schedulerContext, currentEnvironment)
    {
        _clock = clock;
        _variablesService = variablesService;
        _updateServerInfrastructureCommand = updateServerInfrastructureCommand;
        _getLatestServerInfrastructureQuery = getLatestServerInfrastructureQuery;
        _getCurrentServerInfrastructureQuery = getCurrentServerInfrastructureQuery;
        _getInstalledServerInfrastructureQuery = getInstalledServerInfrastructureQuery;
        _logger = logger;
    }

    public override DateTime NextRun => _clock.Today().AddHours(03).AddDays(1);
    public override TimeSpan RunInterval => TimeSpan.FromHours(12);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        if (!_variablesService.GetFeatureState("AUTO_INFRA_UPDATE"))
        {
            return;
        }

        var latestInfo = await _getLatestServerInfrastructureQuery.ExecuteAsync();
        var currentInfo = await _getCurrentServerInfrastructureQuery.ExecuteAsync();
        var installedInfo = await _getInstalledServerInfrastructureQuery.ExecuteAsync();

        if (latestInfo.LatestBuild != currentInfo.CurrentBuild || latestInfo.LatestUpdate > currentInfo.CurrentUpdated || installedInfo.InstalledVersion == "0")
        {
            _logger.LogInfo("Server infrastructure update required");
            await _updateServerInfrastructureCommand.ExecuteAsync();

            var afterVersion = await _getInstalledServerInfrastructureQuery.ExecuteAsync();
            var afterBuild = await _getCurrentServerInfrastructureQuery.ExecuteAsync();
            _logger.LogInfo(
                $"Server infrastructure updated from version {installedInfo.InstalledVersion}.{currentInfo.CurrentBuild} to {afterVersion.InstalledVersion}.{afterBuild.CurrentBuild}"
            );
        }
    }
}
