using System.Collections.Concurrent;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.EventHandlers;

public interface IUksfLoggerEventHandler : IEventHandler
{
    Task FlushAsync(CancellationToken cancellationToken = default);
}

public class UksfLoggerEventHandler(
    IEventBus eventBus,
    ILogContext logContext,
    IAuditLogContext auditLogContext,
    IErrorLogContext errorLogContext,
    ILauncherLogContext launcherLogContext,
    IDiscordLogContext discordLogContext,
    IUksfLogger uksfLogger,
    IObjectIdConversionService objectIdConversionService
) : IUksfLoggerEventHandler
{
    private readonly ConcurrentQueue<DomainBasicLog> _logQueue = new();
    private Task _processingTask = Task.CompletedTask;
    private readonly object _processingLock = new();

    public void EarlyInit()
    {
        eventBus.AsObservable().SubscribeWithAsyncNext<LoggerEventData>(HandleLog, uksfLogger.LogError);
    }

    public void Init() { }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        while (!_logQueue.IsEmpty && !cancellationToken.IsCancellationRequested)
        {
            await ProcessNextLogAsync();
        }
    }

    private Task HandleLog(EventModel eventModel, LoggerEventData logData)
    {
        _logQueue.Enqueue(logData.Log);

        lock (_processingLock)
        {
            if (_processingTask.IsCompleted)
            {
                _processingTask = ProcessNextLogAsync();
            }
        }

        return Task.CompletedTask;
    }

    private async Task ProcessNextLogAsync()
    {
        if (!_logQueue.TryDequeue(out var log))
        {
            return;
        }

        if (log is AuditLog auditLog)
        {
            auditLog.Who = objectIdConversionService.ConvertObjectId(auditLog.Who);
            log = auditLog;
        }

        log.Message = objectIdConversionService.ConvertObjectIds(log.Message).UnescapeForLogging();
        await LogToStorageAsync(log);
    }

    private Task LogToStorageAsync(DomainBasicLog log)
    {
        return log switch
        {
            AuditLog auditLog       => auditLogContext.Add(auditLog),
            LauncherLog launcherLog => launcherLogContext.Add(launcherLog),
            ErrorLog errorLog       => errorLogContext.Add(errorLog),
            DiscordLog discordLog   => discordLogContext.Add(discordLog),
            _                       => logContext.Add(log)
        };
    }
}
