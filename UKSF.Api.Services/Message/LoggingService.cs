using System;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Message {
    public class LoggingService : DataBackedService<ILogDataService>, ILoggingService {
        private readonly IDisplayNameService displayNameService;
        private readonly ISessionService sessionService;

        public LoggingService(ILogDataService data, IDisplayNameService displayNameService, ISessionService sessionService) : base(data) {
            this.displayNameService = displayNameService;
            this.sessionService = sessionService;
        }

        public void Log(string message) {
            Task unused = LogAsync(new BasicLogMessage(message));
        }

        public void Log(BasicLogMessage log) {
            if (log is AuditLogMessage auditLog) {
                auditLog.who = displayNameService.GetDisplayName(auditLog.who);
                log = auditLog;
            }

            log.message = log.message.ConvertObjectIds();
            Task unused = LogAsync(log);
        }

        public void Log(Exception exception) {
            Task unused = LogAsync(exception);
        }

        public void AuditLog(string message, string userId = "") {
            if (string.IsNullOrEmpty(userId)) {
                userId = sessionService.GetContextId();
            }

            AuditLogMessage log = new AuditLogMessage { who = userId, level = LogLevel.INFO, message = message };
            Log(log);
        }

        private async Task LogAsync(BasicLogMessage log) => await LogToStorage(log);

        private async Task LogAsync(Exception exception) => await LogToStorage(new BasicLogMessage(exception));

        private async Task LogToStorage(BasicLogMessage log) {
            switch (log) {
                case AuditLogMessage message:
                    await Data.Add(message);
                    break;
                case LauncherLogMessage message:
                    await Data.Add(message);
                    break;
                case WebLogMessage message:
                    await Data.Add(message);
                    break;
                default:
                    await Data.Add(log);
                    break;
            }
        }
    }
}
