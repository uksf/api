using UKSF.Api.Core.Context;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Instagram.Services;

namespace UKSF.Api.Integrations.Instagram.ScheduledActions;

public interface IActionInstagramImages : ISelfCreatingScheduledAction { }

public class ActionInstagramImages : IActionInstagramImages
{
    private const string ActionName = nameof(ActionInstagramImages);

    private readonly IClock _clock;
    private readonly IInstagramService _instagramService;
    private readonly ISchedulerContext _schedulerContext;
    private readonly ISchedulerService _schedulerService;

    public ActionInstagramImages(ISchedulerContext schedulerContext, IInstagramService instagramService, ISchedulerService schedulerService, IClock clock)
    {
        _schedulerContext = schedulerContext;
        _instagramService = instagramService;
        _schedulerService = schedulerService;
        _clock = clock;
    }

    public string Name => ActionName;

    public Task Run(params object[] parameters)
    {
        return _instagramService.CacheInstagramImages();
    }

    public async Task CreateSelf()
    {
        if (_schedulerContext.GetSingle(x => x.Action == ActionName) == null)
        {
            await _schedulerService.CreateScheduledJob(_clock.Today(), TimeSpan.FromHours(6), ActionName);
        }

        await Run();
    }

    public Task Reset()
    {
        return Task.CompletedTask;
    }
}
