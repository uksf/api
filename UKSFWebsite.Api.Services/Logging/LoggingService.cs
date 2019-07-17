using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Models.Logging;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Logging {
    public class LoggingService : ILoggingService {
        private readonly IHubContext<AdminHub, IAdminClient> adminHub;
        private readonly IMongoDatabase database;

        public LoggingService(IMongoDatabase database, IHubContext<AdminHub, IAdminClient> adminHub) {
            this.database = database;
            this.adminHub = adminHub;
        }

        public async Task LogAsync(BasicLogMessage log) => await LogToStorage(log);

        public async Task LogAsync(Exception exception) => await LogToStorage(new BasicLogMessage(exception));

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
