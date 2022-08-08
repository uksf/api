using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Admin.ScheduledActions;

public interface IActionPruneLogs : ISelfCreatingScheduledAction { }

public class ActionPruneLogs : IActionPruneLogs
{
    private const string ActionName = nameof(ActionPruneLogs);

    private readonly IAuditLogContext _auditLogContext;
    private readonly IClock _clock;
    private readonly IHostEnvironment _currentEnvironment;
    private readonly IErrorLogContext _errorLogContext;
    private readonly ILogContext _logContext;
    private readonly ISchedulerContext _schedulerContext;
    private readonly ISchedulerService _schedulerService;

    public ActionPruneLogs(
        ILogContext logContext,
        IAuditLogContext auditLogContext,
        IErrorLogContext errorLogContext,
        ISchedulerService schedulerService,
        ISchedulerContext schedulerContext,
        IHostEnvironment currentEnvironment,
        IClock clock
    )
    {
        _logContext = logContext;
        _auditLogContext = auditLogContext;
        _errorLogContext = errorLogContext;
        _schedulerService = schedulerService;
        _schedulerContext = schedulerContext;
        _currentEnvironment = currentEnvironment;
        _clock = clock;
    }

    public string Name => ActionName;

    public Task Run(params object[] parameters)
    {
        var now = _clock.UtcNow();
        var logsTask = _logContext.DeleteMany(x => x.Timestamp < now.AddDays(-7));
        var auditLogsTask = _auditLogContext.DeleteMany(x => x.Timestamp < now.AddMonths(-3));
        var errorLogsTask = _errorLogContext.DeleteMany(x => x.Timestamp < now.AddDays(-7));

        Task.WaitAll(logsTask, errorLogsTask, auditLogsTask);
        return Task.CompletedTask;
    }

    public async Task CreateSelf()
    {
        if (_currentEnvironment.IsDevelopment())
        {
            return;
        }

        if (_schedulerContext.GetSingle(x => x.Action == ActionName) == null)
        {
            await _schedulerService.CreateScheduledJob(_clock.Today().AddDays(1), TimeSpan.FromDays(1), ActionName);
        }
    }

    public Task Reset()
    {
        return Task.CompletedTask;
    }
}
