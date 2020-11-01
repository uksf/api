using System;
using System.Threading.Tasks;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Services;
using UKSF.Api.Services.Data;

namespace UKSF.Api.EventHandlers {
    public interface ILoggerEventHandler : IEventHandler { }

    public class LoggerEventHandler : ILoggerEventHandler {
        private readonly IObjectIdConversionService objectIdConversionService;
        private readonly ILogDataService data;
        private readonly ILogger logger;

        public LoggerEventHandler(ILogDataService data, ILogger logger, IObjectIdConversionService objectIdConversionService) {
            this.data = data;
            this.logger = logger;
            this.objectIdConversionService = objectIdConversionService;
        }

        public void Init() {
            logger.AsObservable().Subscribe(HandleLog, logger.LogError);
        }

        private void HandleLog(BasicLog log) {
            Task _ = HandleLogAsync(log);
        }

        private async Task HandleLogAsync(BasicLog log) {
            if (log is AuditLog auditLog) {
                auditLog.who = objectIdConversionService.ConvertObjectId(auditLog.who);
                log = auditLog;
            }

            log.message = objectIdConversionService.ConvertObjectIds(log.message);
            await LogToStorageAsync(log);
        }

        private Task LogToStorageAsync(BasicLog log) {
            return log switch {
                AuditLog auditLog         => data.Add(auditLog),
                LauncherLog launcherLog   => data.Add(launcherLog),
                HttpErrorLog httpErrorLog => data.Add(httpErrorLog),
                _                         => data.Add(log)
            };
        }
    }
}
