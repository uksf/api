using UKSF.Api.ArmaServer.Commands;
using UKSF.Api.ArmaServer.Queries;
using UKSF.Api.Core;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.ScheduledActions;

public interface IActionCheckForServerUpdate : ISelfCreatingScheduledAction;

public class ActionCheckForServerUpdate(
    ISchedulerService schedulerService,
    IHostEnvironment currentEnvironment,
    IClock clock,
    IVariablesService variablesService,
    IUpdateServerInfrastructureCommand updateServerInfrastructureCommand,
    IGetLatestServerInfrastructureQuery getLatestServerInfrastructureQuery,
    IGetCurrentServerInfrastructureQuery getCurrentServerInfrastructureQuery,
    IGetInstalledServerInfrastructureQuery getInstalledServerInfrastructureQuery,
    IUksfLogger logger
) : SelfCreatingScheduledAction(schedulerService, currentEnvironment), IActionCheckForServerUpdate
{
    private const string ActionName = nameof(ActionCheckForServerUpdate);

    public override DateTime NextRun => clock.UkToday().AddHours(04).AddDays(1);
    public override TimeSpan RunInterval => TimeSpan.FromHours(12);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        if (!variablesService.GetFeatureState("AUTO_INFRA_UPDATE"))
        {
            return;
        }

        var latestInfo = await getLatestServerInfrastructureQuery.ExecuteAsync();
        var currentInfo = await getCurrentServerInfrastructureQuery.ExecuteAsync();
        var installedInfo = await getInstalledServerInfrastructureQuery.ExecuteAsync();

        if (latestInfo.LatestBuild != currentInfo.CurrentBuild || latestInfo.LatestUpdate > currentInfo.CurrentUpdated || installedInfo.InstalledVersion == "0")
        {
            logger.LogInfo("Server infrastructure update required");
            await updateServerInfrastructureCommand.ExecuteAsync();

            var afterVersion = await getInstalledServerInfrastructureQuery.ExecuteAsync();
            var afterBuild = await getCurrentServerInfrastructureQuery.ExecuteAsync();
            logger.LogInfo(
                $"Server infrastructure updated from version {installedInfo.InstalledVersion}.{currentInfo.CurrentBuild} to {afterVersion.InstalledVersion}.{afterBuild.CurrentBuild}"
            );
        }
    }
}
