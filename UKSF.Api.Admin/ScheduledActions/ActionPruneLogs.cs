using System;
using System.Threading.Tasks;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Base.Services;

namespace UKSF.Api.Admin.ScheduledActions {
    public interface IActionPruneLogs : ISelfCreatingScheduledAction { }

    public class ActionPruneLogs : IActionPruneLogs {
        public const string ACTION_NAME = nameof(ActionPruneLogs);

        private readonly IAuditLogDataService auditLogDataService;
        private readonly IClock clock;
        private readonly IHttpErrorLogDataService httpErrorLogDataService;
        private readonly ILogDataService logDataService;
        private readonly ISchedulerService schedulerService;

        public ActionPruneLogs(
            ILogDataService logDataService,
            IAuditLogDataService auditLogDataService,
            IHttpErrorLogDataService httpErrorLogDataService,
            ISchedulerService schedulerService,
            IClock clock
        ) {
            this.logDataService = logDataService;
            this.auditLogDataService = auditLogDataService;
            this.httpErrorLogDataService = httpErrorLogDataService;
            this.schedulerService = schedulerService;
            this.clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            DateTime now = clock.UtcNow();
            Task logsTask = logDataService.DeleteMany(x => x.timestamp < now.AddDays(-7));
            Task errorLogsTask = httpErrorLogDataService.DeleteMany(x => x.timestamp < now.AddDays(-7));
            Task auditLogsTask = auditLogDataService.DeleteMany(x => x.timestamp < now.AddMonths(-3));

            Task.WaitAll(logsTask, errorLogsTask, auditLogsTask);
        }

        public async Task CreateSelf() {
            if (schedulerService.Data.GetSingle(x => x.action == ACTION_NAME) == null) {
                await schedulerService.CreateScheduledJob(clock.Today().AddDays(1), TimeSpan.FromDays(1), ACTION_NAME);
            }
        }
    }
}
