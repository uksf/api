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
        private readonly IHubContext<AdminHub, IAdminClient> _hub;
        private readonly IDataEventBus<BasicLog> _logDataEventBus;
        private readonly ILogger _logger;

        public LogDataEventHandler(IDataEventBus<BasicLog> logDataEventBus, IHubContext<AdminHub, IAdminClient> hub, ILogger logger) {
            _logDataEventBus = logDataEventBus;
            _hub = hub;
            _logger = logger;
        }

        public void Init() {
            _logDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => _logger.LogError(exception));
        }

        private async Task HandleEvent(DataEventModel<BasicLog> dataEventModel) {
            if (dataEventModel.Type == DataEventType.ADD) {
                await AddedEvent(dataEventModel.Data);
            }
        }

        private Task AddedEvent(object log) {
            return log switch {
                AuditLog message     => _hub.Clients.All.ReceiveAuditLog(message),
                LauncherLog message  => _hub.Clients.All.ReceiveLauncherLog(message),
                HttpErrorLog message => _hub.Clients.All.ReceiveErrorLog(message),
                BasicLog message     => _hub.Clients.All.ReceiveLog(message),
                _                    => throw new ArgumentOutOfRangeException(nameof(log), "Log type is not valid")
            };
        }
    }
}
