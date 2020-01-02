using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events.Handlers;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Message.Logging;
using UKSFWebsite.Api.Signalr.Hubs.Personnel;
using UKSFWebsite.Api.Signalr.Hubs.Utility;

namespace UKSFWebsite.Api.Events.Handlers {
    public class LogEventHandler : ILogEventHandler {
        private readonly ILogDataService data;
        private readonly IHubContext<AdminHub, IAdminClient> hub;

        public LogEventHandler(ILogDataService data, IHubContext<AdminHub, IAdminClient> hub) {
            this.data = data;
            this.hub = hub;
        }

        public void Init() {
            data.EventBus()
                .Subscribe(
                    async x => {
                        if (x.type == DataEventType.ADD) await AddedEvent(x.data);
                    }
                );
        }

        private async Task AddedEvent(object log) {
            switch (log) {
                case AuditLogMessage message:
                    await hub.Clients.All.ReceiveAuditLog(message);
                    break;
                case LauncherLogMessage message:
                    await hub.Clients.All.ReceiveLauncherLog(message);
                    break;
                case WebLogMessage message:
                    await hub.Clients.All.ReceiveErrorLog(message);
                    break;
                default:
                    BasicLogMessage basicLogMessage = log as BasicLogMessage;
                    await hub.Clients.All.ReceiveLog(basicLogMessage);
                    break;
            }
        }
    }
}
