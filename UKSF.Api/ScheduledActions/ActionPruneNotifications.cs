using UKSF.Api.Core.Context;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ScheduledActions;

public interface IActionPruneNotifications : ISelfCreatingScheduledAction { }

public class ActionPruneNotifications : SelfCreatingScheduledAction, IActionPruneNotifications
{
    private const string ActionName = nameof(ActionPruneNotifications);

    private readonly IClock _clock;
    private readonly INotificationsContext _notificationsContext;

    public ActionPruneNotifications(
        INotificationsContext notificationsContext,
        ISchedulerService schedulerService,
        IHostEnvironment currentEnvironment,
        IClock clock
    ) : base(schedulerService, currentEnvironment)
    {
        _notificationsContext = notificationsContext;
        _clock = clock;
    }

    public override DateTime NextRun => _clock.Today().AddDays(1);
    public override TimeSpan RunInterval => TimeSpan.FromDays(1);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        var now = _clock.UtcNow();
        await _notificationsContext.DeleteMany(x => x.Timestamp < now.AddMonths(-1));
    }
}
