using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Admin.ScheduledActions {
    public interface IActionPruneLogs : ISelfCreatingScheduledAction { }

    public class ActionPruneLogs : IActionPruneLogs {
        private const string ACTION_NAME = nameof(ActionPruneLogs);

        private readonly IAuditLogDataService _auditLogDataService;
        private readonly IClock _clock;
        private readonly IHostEnvironment _currentEnvironment;
        private readonly IHttpErrorLogDataService _httpErrorLogDataService;
        private readonly ILogDataService _logDataService;
        private readonly ISchedulerService _schedulerService;

        public ActionPruneLogs(
            ILogDataService logDataService,
            IAuditLogDataService auditLogDataService,
            IHttpErrorLogDataService httpErrorLogDataService,
            ISchedulerService schedulerService,
            IHostEnvironment currentEnvironment,
            IClock clock
        ) {
            _logDataService = logDataService;
            _auditLogDataService = auditLogDataService;
            _httpErrorLogDataService = httpErrorLogDataService;
            _schedulerService = schedulerService;
            _currentEnvironment = currentEnvironment;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            DateTime now = _clock.UtcNow();
            Task logsTask = _logDataService.DeleteMany(x => x.timestamp < now.AddDays(-7));
            Task auditLogsTask = _auditLogDataService.DeleteMany(x => x.timestamp < now.AddMonths(-3));
            Task errorLogsTask = _httpErrorLogDataService.DeleteMany(x => x.timestamp < now.AddDays(-7));

            Task.WaitAll(logsTask, errorLogsTask, auditLogsTask);
        }

        public async Task CreateSelf() {
            if (_currentEnvironment.IsDevelopment()) return;

            if (_schedulerService.Data.GetSingle(x => x.action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today().AddDays(1), TimeSpan.FromDays(1), ACTION_NAME);
            }
        }
    }
}
