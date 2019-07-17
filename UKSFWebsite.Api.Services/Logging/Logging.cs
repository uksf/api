using System;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Logging;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Logging {
    public class Logging : ILogging {
        private readonly IDisplayNameService displayNameService;
        private readonly ILoggingService loggingService;

        public Logging(ILoggingService loggingService, IDisplayNameService displayNameService) {
            this.loggingService = loggingService;
            this.displayNameService = displayNameService;
        }

        public void Log(string message) {
            Task unused = loggingService.LogAsync(new BasicLogMessage(message));
        }

        public void Log(BasicLogMessage log) {
            if (log is AuditLogMessage auditLog) {
                auditLog.who = displayNameService.GetDisplayName(auditLog.who);
                log = auditLog;
            }

            log.message = log.message.ConvertObjectIds();
            Task unused = loggingService.LogAsync(log);
        }

        public void Log(Exception exception) {
            Task unused = loggingService.LogAsync(exception);
        }
    }
}
