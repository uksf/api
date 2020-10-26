using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.SignalrHubs.Clients;
using UKSF.Api.Admin.SignalrHubs.Hubs;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Logging.Models;
using UKSF.Api.Logging.Services;

namespace UKSF.Api.Admin.EventHandlers {
    public interface ILogEventHandler : IEventHandler { }

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
