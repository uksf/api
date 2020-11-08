using System;
using System.Threading.Tasks;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Base.Services;
using UKSF.Api.Personnel.Context;

namespace UKSF.Api.Personnel.ScheduledActions {
    public interface IActionPruneNotifications : ISelfCreatingScheduledAction { }

    public class ActionPruneNotifications : IActionPruneNotifications {
        public const string ACTION_NAME = nameof(ActionPruneNotifications);

        private readonly IClock clock;
        private readonly INotificationsDataService notificationsDataService;
        private readonly ISchedulerService schedulerService;

        public ActionPruneNotifications(INotificationsDataService notificationsDataService, ISchedulerService schedulerService, IClock clock) {
            this.notificationsDataService = notificationsDataService;
            this.schedulerService = schedulerService;
            this.clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            DateTime now = clock.UtcNow();
            Task notificationsTask = notificationsDataService.DeleteMany(x => x.timestamp < now.AddMonths(-1));

            Task.WaitAll(notificationsTask);
        }

        public async Task CreateSelf() {
            if (schedulerService.Data.GetSingle(x => x.action == ACTION_NAME) == null) {
                await schedulerService.CreateScheduledJob(clock.Today().AddDays(1), TimeSpan.FromDays(1), ACTION_NAME);
            }
        }
    }
}
