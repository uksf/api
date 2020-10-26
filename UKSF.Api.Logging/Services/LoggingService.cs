using System;
using System.Threading.Tasks;
using UKSF.Api.Base.Services;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Logging.Models;
using UKSF.Api.Logging.Services.Data;

namespace UKSF.Api.Logging.Services {
    public interface ILoggingService : IDataBackedService<ILogDataService> {
        void Log(string message);
        void Log(BasicLogMessage log);
        void Log(Exception exception);
        void AuditLog(string message, string userId = "");
    }

    public class LoggingService : DataBackedService<ILogDataService>, ILoggingService {
        private readonly IDisplayNameService displayNameService;
        private readonly IHttpContextService httpContextService;

        public LoggingService(ILogDataService data, IDisplayNameService displayNameService, IHttpContextService httpContextService) : base(data) {
            this.displayNameService = displayNameService;
            this.httpContextService = httpContextService;
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
                userId = httpContextService.GetUserId();
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
