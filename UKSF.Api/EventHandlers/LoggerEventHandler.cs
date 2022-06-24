using System.Threading.Tasks;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.EventHandlers
{
    public interface ILoggerEventHandler : IEventHandler { }

    public class LoggerEventHandler : ILoggerEventHandler
    {
        private readonly IAuditLogContext _auditLogContext;
        private readonly IDiscordLogContext _discordLogContext;
        private readonly IErrorLogContext _errorLogContext;
        private readonly IEventBus _eventBus;
        private readonly ILauncherLogContext _launcherLogContext;
        private readonly ILogContext _logContext;
        private readonly ILogger _logger;
        private readonly IObjectIdConversionService _objectIdConversionService;

        public LoggerEventHandler(
            IEventBus eventBus,
            ILogContext logContext,
            IAuditLogContext auditLogContext,
            IErrorLogContext errorLogContext,
            ILauncherLogContext launcherLogContext,
            IDiscordLogContext discordLogContext,
            ILogger logger,
            IObjectIdConversionService objectIdConversionService
        )
        {
            _eventBus = eventBus;
            _logContext = logContext;
            _auditLogContext = auditLogContext;
            _errorLogContext = errorLogContext;
            _launcherLogContext = launcherLogContext;
            _discordLogContext = discordLogContext;
            _logger = logger;
            _objectIdConversionService = objectIdConversionService;
        }

        public void EarlyInit()
        {
            _eventBus.AsObservable().SubscribeWithAsyncNext<LoggerEventData>(HandleLog, _logger.LogError);
        }

        public void Init()
        {
        }

        private Task HandleLog(EventModel eventModel, LoggerEventData logData)
        {
            var _ = HandleLogAsync(logData.Log);
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
}
