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

        private readonly IAuditLogContext _auditLogContext;
        private readonly IClock _clock;
        private readonly IHostEnvironment _currentEnvironment;
        private readonly IHttpErrorLogContext _httpErrorLogContext;
        private readonly ILogContext _logContext;
        private readonly ISchedulerContext _schedulerContext;
        private readonly ISchedulerService _schedulerService;

        public ActionPruneLogs(
            ILogContext logContext,
            IAuditLogContext auditLogContext,
            IHttpErrorLogContext httpErrorLogContext,
            ISchedulerService schedulerService,
            ISchedulerContext schedulerContext,
            IHostEnvironment currentEnvironment,
            IClock clock
        ) {
            _logContext = logContext;
            _auditLogContext = auditLogContext;
            _httpErrorLogContext = httpErrorLogContext;
            _schedulerService = schedulerService;
            _schedulerContext = schedulerContext;
            _currentEnvironment = currentEnvironment;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            DateTime now = _clock.UtcNow();
            Task logsTask = _logContext.DeleteMany(x => x.Timestamp < now.AddDays(-7));
            Task auditLogsTask = _auditLogContext.DeleteMany(x => x.Timestamp < now.AddMonths(-3));
            Task errorLogsTask = _httpErrorLogContext.DeleteMany(x => x.Timestamp < now.AddDays(-7));

            Task.WaitAll(logsTask, errorLogsTask, auditLogsTask);
        }

        public async Task CreateSelf() {
            if (_currentEnvironment.IsDevelopment()) return;

            if (_schedulerContext.GetSingle(x => x.Action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today().AddDays(1), TimeSpan.FromDays(1), ACTION_NAME);
            }
        }
    }
}
