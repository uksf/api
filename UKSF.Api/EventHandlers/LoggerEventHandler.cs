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
        private readonly IAuditLogDataService _auditLogDataService;
        private readonly IHttpErrorLogDataService _httpErrorLogDataService;
        private readonly ILauncherLogDataService _launcherLogDataService;
        private readonly ILogDataService _logDataService;
        private readonly ILogger _logger;
        private readonly IObjectIdConversionService _objectIdConversionService;

        public LoggerEventHandler(
            ILogDataService logDataService,
            IAuditLogDataService auditLogDataService,
            IHttpErrorLogDataService httpErrorLogDataService,
            ILauncherLogDataService launcherLogDataService,
            ILogger logger,
            IObjectIdConversionService objectIdConversionService
        ) {
            _logDataService = logDataService;
            _auditLogDataService = auditLogDataService;
            _httpErrorLogDataService = httpErrorLogDataService;
            _launcherLogDataService = launcherLogDataService;
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
                auditLog.who = _objectIdConversionService.ConvertObjectId(auditLog.who);
                log = auditLog;
            }

            log.message = _objectIdConversionService.ConvertObjectIds(log.message);
            await LogToStorageAsync(log);
        }

        private Task LogToStorageAsync(BasicLog log) {
            return log switch {
                AuditLog auditLog         => _auditLogDataService.Add(auditLog),
                LauncherLog launcherLog   => _launcherLogDataService.Add(launcherLog),
                HttpErrorLog httpErrorLog => _httpErrorLogDataService.Add(httpErrorLog),
                _                         => _logDataService.Add(log)
            };
        }
    }
}
