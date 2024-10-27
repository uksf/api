using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.EventHandlers;

public interface ILogDataEventHandler : IEventHandler;

public class LogDataEventHandler(IEventBus eventBus, IHubContext<AdminHub, IAdminClient> hub, IUksfLogger logger) : ILogDataEventHandler
{
    public void EarlyInit() { }

    public void Init()
    {
        eventBus.AsObservable().SubscribeWithAsyncNext<LoggerEventData>(HandleEvent, logger.LogError);
    }

    private async Task HandleEvent(EventModel eventModel, LoggerEventData logData)
    {
        await hub.Clients.All.ReceiveLog();
    }
}
