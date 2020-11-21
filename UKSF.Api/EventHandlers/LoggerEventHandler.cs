using System;
using System.Threading.Tasks;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.EventHandlers {
    public interface ILoggerEventHandler : IEventHandler { }

    public class LoggerEventHandler : ILoggerEventHandler {
        private readonly IAuditLogContext _auditLogContext;
        private readonly IDiscordLogContext _discordLogContext;
        private readonly IHttpErrorLogContext _httpErrorLogContext;
        private readonly ILauncherLogContext _launcherLogContext;
        private readonly ILogContext _logContext;
        private readonly ILogger _logger;
        private readonly IObjectIdConversionService _objectIdConversionService;

        public LoggerEventHandler(
            ILogContext logContext,
            IAuditLogContext auditLogContext,
            IHttpErrorLogContext httpErrorLogContext,
            ILauncherLogContext launcherLogContext,
            IDiscordLogContext discordLogContext,
            ILogger logger,
            IObjectIdConversionService objectIdConversionService
        ) {
            _logContext = logContext;
            _auditLogContext = auditLogContext;
            _httpErrorLogContext = httpErrorLogContext;
            _launcherLogContext = launcherLogContext;
            _discordLogContext = discordLogContext;
            _logger = logger;
            _objectIdConversionService = objectIdConversionService;
        }

        public void Init() {
            _logger.AsObservable().Subscribe(HandleLog, _logger.LogError);
        }

        private void HandleLog(BasicLog log) {
            Task _ = HandleLogAsync(log);
        }

        private async Task HandleLogAsync(BasicLog log) {
            if (log is AuditLog auditLog) {
                auditLog.Who = _objectIdConversionService.ConvertObjectId(auditLog.Who);
                log = auditLog;
            }

            log.Message = _objectIdConversionService.ConvertObjectIds(log.Message);
            await LogToStorageAsync(log);
        }

        private Task LogToStorageAsync(BasicLog log) {
            return log switch {
                AuditLog auditLog         => _auditLogContext.Add(auditLog),
                LauncherLog launcherLog   => _launcherLogContext.Add(launcherLog),
                HttpErrorLog httpErrorLog => _httpErrorLogContext.Add(httpErrorLog),
                DiscordLog discordLog     => _discordLogContext.Add(discordLog),
                _                         => _logContext.Add(log)
            };
        }
    }
}
