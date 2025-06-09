using UKSF.Api.Core.Context;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Services;

namespace UKSF.Api.Integrations.Teamspeak.ScheduledActions;

public interface IActionTeamspeakSnapshot : ISelfCreatingScheduledAction;

public class ActionTeamspeakSnapshot(
    ISchedulerContext schedulerContext,
    ITeamspeakService teamspeakService,
    ISchedulerService schedulerService,
    IHostEnvironment currentEnvironment,
    IClock clock
) : SelfCreatingScheduledAction(schedulerService, currentEnvironment), IActionTeamspeakSnapshot
{
    private const string ActionName = nameof(ActionTeamspeakSnapshot);

    private readonly IHostEnvironment _currentEnvironment = currentEnvironment;
    private readonly ISchedulerContext _schedulerContext = schedulerContext;
    private readonly ISchedulerService _schedulerService = schedulerService;
    private readonly ITeamspeakService _teamspeakService = teamspeakService;

    public override DateTime NextRun => clock.Today();
    public override TimeSpan RunInterval => TimeSpan.FromMinutes(5);
    public override string Name => ActionName;

    public override Task Run(params object[] parameters)
    {
        return Task.CompletedTask;
        // return _teamspeakService.StoreTeamspeakServerSnapshot();
    }
}
