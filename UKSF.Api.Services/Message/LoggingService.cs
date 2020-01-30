using System;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Services.Utility;

namespace UKSF.Api.Services.Message {
    public class LoggingService : ILoggingService {
        private readonly ILogDataService data;
        private readonly IDisplayNameService displayNameService;

        public LoggingService(ILogDataService data, IDisplayNameService displayNameService) {
            this.data = data;
            this.displayNameService = displayNameService;
        }

        public ILogDataService Data() => data;

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

        private async Task LogAsync(BasicLogMessage log) => await LogToStorage(log);

        private async Task LogAsync(Exception exception) => await LogToStorage(new BasicLogMessage(exception));

        private async Task LogToStorage(BasicLogMessage log) {
            switch (log) {
                case AuditLogMessage message:
                    await data.Add(message);
                    break;
                case LauncherLogMessage message:
                    await data.Add(message);
                    break;
                case WebLogMessage message:
                    await data.Add(message);
                    break;
                default:
                    await data.Add(log);
                    break;
            }
        }
    }
}
