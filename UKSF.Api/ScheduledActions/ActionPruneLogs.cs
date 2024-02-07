using UKSF.Api.Core.Context;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ScheduledActions;

public interface IActionPruneLogs : ISelfCreatingScheduledAction { }

public class ActionPruneLogs : SelfCreatingScheduledAction, IActionPruneLogs
{
    private const string ActionName = nameof(ActionPruneLogs);

    private readonly IAuditLogContext _auditLogContext;
    private readonly IClock _clock;
    private readonly IErrorLogContext _errorLogContext;
    private readonly ILogContext _logContext;

    public ActionPruneLogs(
        ILogContext logContext,
        IAuditLogContext auditLogContext,
        IErrorLogContext errorLogContext,
        ISchedulerService schedulerService,
        IHostEnvironment currentEnvironment,
        IClock clock
    ) : base(schedulerService, currentEnvironment)
    {
        _logContext = logContext;
        _auditLogContext = auditLogContext;
        _errorLogContext = errorLogContext;
        _clock = clock;
    }

    public override DateTime NextRun => _clock.Today().AddDays(1);
    public override TimeSpan RunInterval => TimeSpan.FromDays(1);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        var now = _clock.UtcNow();
        var logsTask = _logContext.DeleteMany(x => x.Timestamp < now.AddDays(-7));
        var auditLogsTask = _auditLogContext.DeleteMany(x => x.Timestamp < now.AddMonths(-3));
        var errorLogsTask = _errorLogContext.DeleteMany(x => x.Timestamp < now.AddDays(-7));

        await Task.WhenAll(logsTask, errorLogsTask, auditLogsTask);
    }
}
