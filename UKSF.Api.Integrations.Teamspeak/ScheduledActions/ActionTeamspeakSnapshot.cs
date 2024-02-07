using UKSF.Api.Core.Context;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Services;

namespace UKSF.Api.Integrations.Teamspeak.ScheduledActions;

public interface IActionTeamspeakSnapshot : ISelfCreatingScheduledAction { }

public class ActionTeamspeakSnapshot : SelfCreatingScheduledAction, IActionTeamspeakSnapshot
{
    private const string ActionName = nameof(ActionTeamspeakSnapshot);

    private readonly IClock _clock;
    private readonly IHostEnvironment _currentEnvironment;
    private readonly ISchedulerContext _schedulerContext;
    private readonly ISchedulerService _schedulerService;
    private readonly ITeamspeakService _teamspeakService;

    public ActionTeamspeakSnapshot(
        ISchedulerContext schedulerContext,
        ITeamspeakService teamspeakService,
        ISchedulerService schedulerService,
        IHostEnvironment currentEnvironment,
        IClock clock
    ) : base(schedulerService, currentEnvironment)
    {
        _schedulerContext = schedulerContext;
        _teamspeakService = teamspeakService;
        _schedulerService = schedulerService;
        _currentEnvironment = currentEnvironment;
        _clock = clock;
    }

    public override DateTime NextRun => _clock.Today();
    public override TimeSpan RunInterval => TimeSpan.FromMinutes(5);
    public override string Name => ActionName;

    public override Task Run(params object[] parameters)
    {
        return Task.CompletedTask;
        // return _teamspeakService.StoreTeamspeakServerSnapshot();
    }
}
