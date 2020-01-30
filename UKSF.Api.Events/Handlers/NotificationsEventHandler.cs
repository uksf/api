using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;
using UKSF.Api.Signalr.Hubs.Message;

namespace UKSF.Api.Events.Handlers {
    public class NotificationsEventHandler : INotificationsEventHandler {
        private readonly INotificationsDataService data;
        private readonly IHubContext<NotificationHub, INotificationsClient> hub;

        public NotificationsEventHandler(INotificationsDataService data, IHubContext<NotificationHub, INotificationsClient> hub) {
            this.data = data;
            this.hub = hub;
        }

        public void Init() {
            data.EventBus()
                .Subscribe(
                    async x => {
                        if (x.type == DataEventType.ADD) await AddedEvent(x.data as Notification);
                    }
                );
        }

        private async Task AddedEvent(Notification notification) {
            await hub.Clients.Group(notification.owner).ReceiveNotification(notification);
        }
    }
}
