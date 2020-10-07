using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Signalr.Hubs.Utility;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class LogEventHandler : ILogEventHandler {
        private readonly IDataEventBus<BasicLogMessage> logDataEventBus;
        private readonly IHubContext<AdminHub, IAdminClient> hub;
        private readonly ILoggingService loggingService;

        public LogEventHandler(IDataEventBus<BasicLogMessage> logDataEventBus, IHubContext<AdminHub, IAdminClient> hub, ILoggingService loggingService) {
            this.logDataEventBus = logDataEventBus;
            this.hub = hub;
            this.loggingService = loggingService;
        }

        public void Init() {
            logDataEventBus.AsObservable().SubscribeAsync(HandleEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleEvent(DataEventModel<BasicLogMessage> dataEventModel) {
            if (dataEventModel.type == DataEventType.ADD) {
                await AddedEvent(dataEventModel.data);
            }
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
                case BasicLogMessage message:
                    await hub.Clients.All.ReceiveLog(message);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(log), "Log type is not valid");
            }
        }
    }
}
