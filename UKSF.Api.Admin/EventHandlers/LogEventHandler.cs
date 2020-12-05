using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.Signalr.Clients;
using UKSF.Api.Admin.Signalr.Hubs;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Admin.EventHandlers {
    public interface ILogDataEventHandler : IEventHandler { }

    public class LogDataEventHandler : ILogDataEventHandler {
        private readonly IEventBus _eventBus;
        private readonly IHubContext<AdminHub, IAdminClient> _hub;
        private readonly ILogger _logger;

        public LogDataEventHandler(IEventBus eventBus, IHubContext<AdminHub, IAdminClient> hub, ILogger logger) {
            _eventBus = eventBus;
            _hub = hub;
            _logger = logger;
        }

        public void Init() {
            _eventBus.AsObservable().SubscribeWithAsyncNext<BasicLog>(HandleEvent, _logger.LogError);
        }

        private async Task HandleEvent(EventModel eventModel, BasicLog log) {
            if (eventModel.EventType == EventType.ADD) {
                await AddedEvent(log);
            }
        }

        private Task AddedEvent(BasicLog log) {
            return log switch {
                AuditLog message     => _hub.Clients.All.ReceiveAuditLog(message),
                LauncherLog message  => _hub.Clients.All.ReceiveLauncherLog(message),
                HttpErrorLog message => _hub.Clients.All.ReceiveErrorLog(message),
                { } message          => _hub.Clients.All.ReceiveLog(message),
                _                    => throw new ArgumentOutOfRangeException(nameof(log), "Log type is not valid")
            };
        }
    }
}
