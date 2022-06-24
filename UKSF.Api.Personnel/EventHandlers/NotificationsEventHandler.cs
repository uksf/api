using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Personnel.EventHandlers
{
    public interface INotificationsEventHandler : IEventHandler { }

    public class NotificationsEventHandler : INotificationsEventHandler
    {
        private readonly IEventBus _eventBus;
        private readonly IHubContext<NotificationHub, INotificationsClient> _hub;
        private readonly ILogger _logger;

        public NotificationsEventHandler(IEventBus eventBus, IHubContext<NotificationHub, INotificationsClient> hub, ILogger logger)
        {
            _eventBus = eventBus;
            _hub = hub;
            _logger = logger;
        }

        public void EarlyInit() { }

        public void Init()
        {
            _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<Notification>>(HandleEvent, _logger.LogError);
        }

        private async Task HandleEvent(EventModel eventModel, ContextEventData<Notification> contextEventData)
        {
            if (eventModel.EventType == EventType.ADD)
            {
                await AddedEvent(contextEventData.Data);
            }
        }

        private async Task AddedEvent(Notification notification)
        {
            await _hub.Clients.Group(notification.Owner).ReceiveNotification(notification);
        }
    }
}
