using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.Signalr.Clients;
using UKSF.Api.Admin.Signalr.Hubs;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Admin.EventHandlers {
    public interface ILogDataEventHandler : IEventHandler { }

    public class LogDataEventHandler : ILogDataEventHandler {
        private readonly IDataEventBus<BasicLog> logDataEventBus;
        private readonly IHubContext<AdminHub, IAdminClient> hub;
        private readonly ILogger logger;

        public LogDataEventHandler(IDataEventBus<BasicLog> logDataEventBus, IHubContext<AdminHub, IAdminClient> hub, ILogger logger) {
            this.logDataEventBus = logDataEventBus;
            this.hub = hub;
            this.logger = logger;
        }

        public void Init() {
            logDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => logger.LogError(exception));
        }

        private async Task HandleEvent(DataEventModel<BasicLog> dataEventModel) {
            if (dataEventModel.type == DataEventType.ADD) {
                await AddedEvent(dataEventModel.data);
            }
        }

        private Task AddedEvent(object log) {
            return log switch {
                AuditLog message     => hub.Clients.All.ReceiveAuditLog(message),
                LauncherLog message  => hub.Clients.All.ReceiveLauncherLog(message),
                HttpErrorLog message => hub.Clients.All.ReceiveErrorLog(message),
                BasicLog message     => hub.Clients.All.ReceiveLog(message),
                _                    => throw new ArgumentOutOfRangeException(nameof(log), "Log type is not valid")
            };
        }
    }
}
