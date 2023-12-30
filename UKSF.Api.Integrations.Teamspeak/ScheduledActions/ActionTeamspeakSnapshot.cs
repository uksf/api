using UKSF.Api.Core.Context;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Services;

namespace UKSF.Api.Integrations.Teamspeak.ScheduledActions;

public interface IActionTeamspeakSnapshot : ISelfCreatingScheduledAction { }

public class ActionTeamspeakSnapshot : IActionTeamspeakSnapshot
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
    )
    {
        _schedulerContext = schedulerContext;
        _teamspeakService = teamspeakService;
        _schedulerService = schedulerService;
        _currentEnvironment = currentEnvironment;
        _clock = clock;
    }

    public string Name => ActionName;

    public Task Run(params object[] parameters)
    {
        return Task.CompletedTask;
        // return _teamspeakService.StoreTeamspeakServerSnapshot();
    }

    public async Task CreateSelf()
    {
        if (_currentEnvironment.IsDevelopment())
        {
            return;
        }

        if (_schedulerContext.GetSingle(x => x.Action == ActionName) == null)
        {
            await _schedulerService.CreateScheduledJob(_clock.Today().AddMinutes(5), TimeSpan.FromMinutes(5), ActionName);
        }
    }

    public Task Reset()
    {
        return Task.CompletedTask;
    }
}
