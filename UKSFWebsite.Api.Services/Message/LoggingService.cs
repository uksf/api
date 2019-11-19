using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Models.Message.Logging;
using UKSFWebsite.Api.Services.Utility;
using UKSFWebsite.Api.Signalr.Hubs.Utility;

namespace UKSFWebsite.Api.Services.Message {
    public class LoggingService : ILoggingService {
        private readonly IHubContext<AdminHub, IAdminClient> adminHub;
        private readonly IMongoDatabase database;
        private readonly IDisplayNameService displayNameService;

        public LoggingService(IMongoDatabase database, IDisplayNameService displayNameService, IHubContext<AdminHub, IAdminClient> adminHub) {
            this.database = database;
            this.displayNameService = displayNameService;
            this.adminHub = adminHub;
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

        private async Task LogAsync(BasicLogMessage log) => await LogToStorage(log);

        private async Task LogAsync(Exception exception) => await LogToStorage(new BasicLogMessage(exception));

        private async Task LogToStorage(BasicLogMessage log) {
            switch (log) {
                case WebLogMessage message:
                    await database.GetCollection<WebLogMessage>("errorLogs").InsertOneAsync(message);
                    await adminHub.Clients.All.ReceiveErrorLog(message);
                    break;
                case AuditLogMessage message:
                    await database.GetCollection<AuditLogMessage>("auditLogs").InsertOneAsync(message);
                    await adminHub.Clients.All.ReceiveAuditLog(message);
                    break;
                case LauncherLogMessage message:
                    await database.GetCollection<LauncherLogMessage>("launcherLogs").InsertOneAsync(message);
                    await adminHub.Clients.All.ReceiveLauncherLog(message);
                    break;
                default:
                    await database.GetCollection<BasicLogMessage>("logs").InsertOneAsync(log);
                    await adminHub.Clients.All.ReceiveLog(log);
                    break;
            }
        }
    }
}
