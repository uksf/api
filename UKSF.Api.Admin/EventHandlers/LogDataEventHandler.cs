using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.Signalr.Clients;
using UKSF.Api.Admin.Signalr.Hubs;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Admin.EventHandlers;

public interface ILogDataEventHandler : IEventHandler { }

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
