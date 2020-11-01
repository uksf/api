using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.SignalrHubs.Clients;
using UKSF.Api.Personnel.SignalrHubs.Hubs;

namespace UKSF.Api.Personnel.EventHandlers {
    public interface INotificationsEventHandler : IEventHandler { }

    public class NotificationsEventHandler : INotificationsEventHandler {
        private readonly IHubContext<NotificationHub, INotificationsClient> hub;
        private readonly ILogger logger;
        private readonly IDataEventBus<Notification> notificationDataEventBus;

        public NotificationsEventHandler(IDataEventBus<Notification> notificationDataEventBus, IHubContext<NotificationHub, INotificationsClient> hub, ILogger logger) {
            this.notificationDataEventBus = notificationDataEventBus;
            this.hub = hub;
            this.logger = logger;
        }

        public void Init() {
            notificationDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => logger.LogError(exception));
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
