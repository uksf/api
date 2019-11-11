using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Services.Hubs;

namespace UKSFWebsite.Api.Events.Handlers {
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
