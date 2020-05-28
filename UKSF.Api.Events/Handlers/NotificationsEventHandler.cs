using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;
using UKSF.Api.Signalr.Hubs.Message;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class NotificationsEventHandler : INotificationsEventHandler {
        private readonly INotificationsDataService data;
        private readonly IHubContext<NotificationHub, INotificationsClient> hub;
        private readonly ILoggingService loggingService;

        public NotificationsEventHandler(INotificationsDataService data, IHubContext<NotificationHub, INotificationsClient> hub, ILoggingService loggingService) {
            this.data = data;
            this.hub = hub;
            this.loggingService = loggingService;
        }

        public void Init() {
            data.EventBus().SubscribeAsync(HandleEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleEvent(DataEventModel<INotificationsDataService> x) {
            if (x.type == DataEventType.ADD) {
                await AddedEvent(x.data as Notification);
            }
        }

        private async Task AddedEvent(Notification notification) {
            await hub.Clients.Group(notification.owner).ReceiveNotification(notification);
        }
    }
}
