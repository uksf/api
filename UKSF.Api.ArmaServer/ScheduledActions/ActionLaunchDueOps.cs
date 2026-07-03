using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.ScheduledActions;

public interface IActionLaunchDueOps : ISelfCreatingScheduledAction;

public class ActionLaunchDueOps(
    ISchedulerService schedulerService,
    IHostEnvironment currentEnvironment,
    IClock clock,
    IOpsContext opsContext,
    IOpsService opsService,
    IGameServersService gameServersService,
    IUksfLogger logger
) : SelfCreatingScheduledAction(schedulerService, currentEnvironment), IActionLaunchDueOps
{
    public const string LaunchedByScheduler = "Scheduler";
    private static readonly TimeSpan GraceWindow = TimeSpan.FromMinutes(30);
    private const string ActionName = nameof(ActionLaunchDueOps);

    public override DateTime NextRun => NextRunAfter(clock.UtcNow());
    public override TimeSpan RunInterval => TimeSpan.FromMinutes(1);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        var now = clock.UtcNow();
        var dueOps = opsContext.Get(x => x.Status == OpStatus.Scheduled && x.AutoLaunch && x.LaunchedAt == null && x.ScheduledTime <= now).ToList();

        foreach (var op in dueOps)
        {
            if (now - op.ScheduledTime > GraceWindow)
            {
                logger.LogInfo($"Op '{op.Title}' missed its auto-launch window (scheduled {op.ScheduledTime:O}), skipping - launch manually");
                continue;
            }

            try
            {
                await opsService.LaunchOpAsync(op, LaunchedByScheduler);
                logger.LogAudit($"Op '{op.Title}' auto-launched '{op.MissionName}' on '{gameServersService.GetServer(op.ServerId).Name}'");
            }
            catch (Exception ex)
            {
                logger.LogError($"Auto-launch failed for op '{op.Title}', will retry next tick", ex);
            }
        }
    }
}
