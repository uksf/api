using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Instagram.Services;

namespace UKSF.Api.Integrations.Instagram.ScheduledActions;

public interface IActionInstagramImages : ISelfCreatingScheduledAction { }

public class ActionInstagramImages : SelfCreatingScheduledAction, IActionInstagramImages
{
    private const string ActionName = nameof(ActionInstagramImages);

    private readonly IClock _clock;
    private readonly IInstagramService _instagramService;

    public ActionInstagramImages(
        IInstagramService instagramService,
        ISchedulerService schedulerService,
        IHostEnvironment currentEnvironment,
        IClock clock
    ) : base(schedulerService, currentEnvironment)
    {
        _instagramService = instagramService;
        _clock = clock;
    }

    public override DateTime NextRun => _clock.UkToday();
    public override TimeSpan RunInterval => TimeSpan.FromHours(12);
    public override string Name => ActionName;
    public override bool RunOnCreate => true;

    public override Task Run(params object[] parameters)
    {
        return _instagramService.CacheInstagramImages();
    }
}
