using UKSF.Api.Core.Context;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Instagram.Services;

namespace UKSF.Api.Integrations.Instagram.ScheduledActions;

public interface IActionInstagramToken : ISelfCreatingScheduledAction;

public class ActionInstagramToken : SelfCreatingScheduledAction, IActionInstagramToken
{
    private const string ActionName = nameof(ActionInstagramToken);

    private readonly IClock _clock;
    private readonly IInstagramService _instagramService;
    private readonly ISchedulerContext _schedulerContext;

    public ActionInstagramToken(
        ISchedulerContext schedulerContext,
        IInstagramService instagramService,
        ISchedulerService schedulerService,
        IHostEnvironment currentEnvironment,
        IClock clock
    ) : base(schedulerService, currentEnvironment)
    {
        _schedulerContext = schedulerContext;
        _instagramService = instagramService;
        _clock = clock;
    }

    public override DateTime NextRun => _clock.Today().AddDays(45);
    public override TimeSpan RunInterval => TimeSpan.FromDays(45);
    public override string Name => ActionName;

    public override Task Run(params object[] parameters)
    {
        return _instagramService.RefreshAccessToken();
    }

    public override async Task Reset()
    {
        var job = _schedulerContext.GetSingle(x => x.Action == ActionName);
        await _schedulerContext.Delete(job.Id);

        await CreateSelf();
        await Run();
    }
}
