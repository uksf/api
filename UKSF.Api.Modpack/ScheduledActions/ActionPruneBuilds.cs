using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;

namespace UKSF.Api.Modpack.ScheduledActions;

public interface IActionPruneBuilds : ISelfCreatingScheduledAction;

public class ActionPruneBuilds : SelfCreatingScheduledAction, IActionPruneBuilds
{
    private const string ActionName = nameof(ActionPruneBuilds);
    private readonly IBuildsContext _buildsContext;

    private readonly IClock _clock;

    public ActionPruneBuilds(
        IBuildsContext buildsContext,
        ISchedulerService schedulerService,
        IHostEnvironment currentEnvironment,
        IClock clock
    ) : base(schedulerService, currentEnvironment)
    {
        _buildsContext = buildsContext;
        _clock = clock;
    }

    public override DateTime NextRun => _clock.Today().AddDays(1);
    public override TimeSpan RunInterval => TimeSpan.FromDays(1);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        var threshold = _buildsContext.Get(x => x.Environment == GameEnvironment.Development).Select(x => x.BuildNumber).MaxBy(x => x) - 100;
        await _buildsContext.DeleteMany(x => x.Environment == GameEnvironment.Development && x.BuildNumber < threshold);
    }
}
