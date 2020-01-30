using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Signalr.Hubs.Utility;

namespace UKSF.Api.Events.Handlers {
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
