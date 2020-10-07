using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;
using UKSF.Api.Signalr.Hubs.Message;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class NotificationsEventHandler : INotificationsEventHandler {
        private readonly IHubContext<NotificationHub, INotificationsClient> hub;
        private readonly ILoggingService loggingService;
        private readonly IDataEventBus<Notification> notificationDataEventBus;

        public NotificationsEventHandler(IDataEventBus<Notification> notificationDataEventBus, IHubContext<NotificationHub, INotificationsClient> hub, ILoggingService loggingService) {
            this.notificationDataEventBus = notificationDataEventBus;
            this.hub = hub;
            this.loggingService = loggingService;
        }

        public void Init() {
            notificationDataEventBus.AsObservable().SubscribeAsync(HandleEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleEvent(DataEventModel<Notification> dataEventModel) {
            if (dataEventModel.type == DataEventType.ADD) {
                await AddedEvent(dataEventModel.data as Notification);
            }
        }

        private async Task AddedEvent(Notification notification) {
            await hub.Clients.Group(notification.owner).ReceiveNotification(notification);
        }
    }
}
