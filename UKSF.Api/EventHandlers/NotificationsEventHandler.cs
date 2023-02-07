using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Signalr.Clients;
using UKSF.Api.Core.Signalr.Hubs;

namespace UKSF.Api.EventHandlers;

public interface INotificationsEventHandler : IEventHandler { }

public class NotificationsEventHandler : INotificationsEventHandler
{
    private readonly IEventBus _eventBus;
    private readonly IHubContext<NotificationHub, INotificationsClient> _hub;
    private readonly IUksfLogger _logger;

    public NotificationsEventHandler(IEventBus eventBus, IHubContext<NotificationHub, INotificationsClient> hub, IUksfLogger logger)
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
