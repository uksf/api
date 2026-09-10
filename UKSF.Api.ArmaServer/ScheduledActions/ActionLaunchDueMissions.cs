using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.ScheduledActions;

public interface IActionLaunchDueMissions : ISelfCreatingScheduledAction;

public class ActionLaunchDueMissions(
    ISchedulerService schedulerService,
    IHostEnvironment currentEnvironment,
    IClock clock,
    ICampaignMissionsContext campaignMissionsContext,
    ICampaignMissionsService campaignMissionsService,
    IGameServersService gameServersService,
    IUksfLogger logger
) : SelfCreatingScheduledAction(schedulerService, currentEnvironment), IActionLaunchDueMissions
{
    public const string LaunchedByScheduler = "Scheduler";
    private static readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(30);
    private const string ActionName = nameof(ActionLaunchDueMissions);

    public override DateTime NextRun => NextRunAfter(clock.UtcNow());
    public override TimeSpan RunInterval => TimeSpan.FromMinutes(1);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        var now = clock.UtcNow();
        var dueMissions = campaignMissionsContext.Get(x => x.Status == MissionStatus.Scheduled && x.AutoLaunch && x.LaunchedAt == null && x.ScheduledTime <= now).ToList();

        foreach (var mission in dueMissions)
        {
            if (now - mission.ScheduledTime > GraceWindow)
            {
                logger.LogInfo($"Mission '{mission.Title}' missed its auto-launch window (scheduled {mission.ScheduledTime:O}), skipping - launch manually");
                continue;
            }

            try
            {
                await campaignMissionsService.LaunchMissionAsync(mission, LaunchedByScheduler);
                logger.LogAudit($"Mission '{mission.Title}' auto-launched '{mission.MissionName}' on '{gameServersService.GetServer(mission.ServerId).Name}'");
            }
            catch (Exception ex)
            {
                logger.LogError($"Auto-launch failed for mission '{mission.Title}', will retry next tick", ex);
            }
        }
    }
}
