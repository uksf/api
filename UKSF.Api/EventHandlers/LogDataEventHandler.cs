using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.EventHandlers;

public interface ILogDataEventHandler : IEventHandler;

public class LogDataEventHandler : ILogDataEventHandler
{
    private readonly IEventBus _eventBus;
    private readonly IHubContext<AdminHub, IAdminClient> _hub;
    private readonly IUksfLogger _logger;

    public LogDataEventHandler(IEventBus eventBus, IHubContext<AdminHub, IAdminClient> hub, IUksfLogger logger)
    {
        _eventBus = eventBus;
        _hub = hub;
        _logger = logger;
    }

    public void EarlyInit() { }

    public void Init()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<LoggerEventData>(HandleEvent, _logger.LogError);
    }

    private async Task HandleEvent(EventModel eventModel, LoggerEventData logData)
    {
        await _hub.Clients.All.ReceiveLog();
    }
}
