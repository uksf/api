using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Personnel.ScheduledActions {
    public interface IActionPruneNotifications : ISelfCreatingScheduledAction { }

    public class ActionPruneNotifications : IActionPruneNotifications {
        private const string ACTION_NAME = nameof(ActionPruneNotifications);

        private readonly IClock _clock;
        private readonly IHostEnvironment _currentEnvironment;
        private readonly INotificationsContext _notificationsContext;
        private readonly ISchedulerContext _schedulerContext;
        private readonly ISchedulerService _schedulerService;

        public ActionPruneNotifications(
            ISchedulerContext schedulerContext,
            INotificationsContext notificationsContext,
            ISchedulerService schedulerService,
            IHostEnvironment currentEnvironment,
            IClock clock
        ) {
            _schedulerContext = schedulerContext;
            _notificationsContext = notificationsContext;
            _schedulerService = schedulerService;
            _currentEnvironment = currentEnvironment;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            DateTime now = _clock.UtcNow();
            Task notificationsTask = _notificationsContext.DeleteMany(x => x.Timestamp < now.AddMonths(-1));

            Task.WaitAll(notificationsTask);
        }

        public async Task CreateSelf() {
            if (_currentEnvironment.IsDevelopment()) return;

            if (_schedulerContext.GetSingle(x => x.Action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today().AddDays(1), TimeSpan.FromDays(1), ACTION_NAME);
            }
        }
    }
}
