using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.EventHandlers;

public interface IUksfLoggerEventHandler : IEventHandler { }

public class UksfLoggerEventHandler : IUksfLoggerEventHandler
{
    private readonly IAuditLogContext _auditLogContext;
    private readonly IDiscordLogContext _discordLogContext;
    private readonly IErrorLogContext _errorLogContext;
    private readonly IEventBus _eventBus;
    private readonly ILauncherLogContext _launcherLogContext;
    private readonly ILogContext _logContext;
    private readonly IObjectIdConversionService _objectIdConversionService;
    private readonly IUksfLogger _uksfLogger;

    public UksfLoggerEventHandler(
        IEventBus eventBus,
        ILogContext logContext,
        IAuditLogContext auditLogContext,
        IErrorLogContext errorLogContext,
        ILauncherLogContext launcherLogContext,
        IDiscordLogContext discordLogContext,
        IUksfLogger uksfLogger,
        IObjectIdConversionService objectIdConversionService
    )
    {
        _eventBus = eventBus;
        _logContext = logContext;
        _auditLogContext = auditLogContext;
        _errorLogContext = errorLogContext;
        _launcherLogContext = launcherLogContext;
        _discordLogContext = discordLogContext;
        _uksfLogger = uksfLogger;
        _objectIdConversionService = objectIdConversionService;
    }

    public void EarlyInit()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<LoggerEventData>(HandleLog, _uksfLogger.LogError);
    }

    public void Init() { }

    private Task HandleLog(EventModel eventModel, LoggerEventData logData)
    {
        _ = HandleLogAsync(logData.Log);
        return Task.CompletedTask;
    }

    private async Task HandleLogAsync(BasicLog log)
    {
        if (log is AuditLog auditLog)
        {
            auditLog.Who = _objectIdConversionService.ConvertObjectId(auditLog.Who);
            log = auditLog;
        }

        log.Message = _objectIdConversionService.ConvertObjectIds(log.Message);
        await LogToStorageAsync(log);
    }

    private Task LogToStorageAsync(BasicLog log)
    {
        return log switch
        {
            AuditLog auditLog       => _auditLogContext.Add(auditLog),
            LauncherLog launcherLog => _launcherLogContext.Add(launcherLog),
            ErrorLog errorLog       => _errorLogContext.Add(errorLog),
            DiscordLog discordLog   => _discordLogContext.Add(discordLog),
            _                       => _logContext.Add(log)
        };
    }
}
