using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Personnel.EventHandlers {
    public interface INotificationsEventHandler : IEventHandler { }

    public class NotificationsEventHandler : INotificationsEventHandler {
        private readonly IHubContext<NotificationHub, INotificationsClient> _hub;
        private readonly ILogger _logger;
        private readonly IDataEventBus<Notification> _notificationDataEventBus;

        public NotificationsEventHandler(IDataEventBus<Notification> notificationDataEventBus, IHubContext<NotificationHub, INotificationsClient> hub, ILogger logger) {
            _notificationDataEventBus = notificationDataEventBus;
            _hub = hub;
            _logger = logger;
        }

        public void Init() {
            _notificationDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => _logger.LogError(exception));
        }

        private async Task HandleEvent(DataEventModel<Notification> dataEventModel) {
            if (dataEventModel.Type == DataEventType.ADD) {
                await AddedEvent(dataEventModel.Data as Notification);
            }
        }

        private async Task AddedEvent(Notification notification) {
            await _hub.Clients.Group(notification.Owner).ReceiveNotification(notification);
        }
    }
}
