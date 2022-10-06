using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.EventHandlers;

public interface ICommandRequestEventHandler : IEventHandler { }

public class CommandRequestEventHandler : ICommandRequestEventHandler
{
    private readonly IEventBus _eventBus;
    private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> _hub;
    private readonly IUksfLogger _logger;

    public CommandRequestEventHandler(IEventBus eventBus, IHubContext<CommandRequestsHub, ICommandRequestsClient> hub, IUksfLogger logger)
    {
        _eventBus = eventBus;
        _hub = hub;
        _logger = logger;
    }

    public void EarlyInit() { }

    public void Init()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<CommandRequest>>(HandleEvent, _logger.LogError);
    }

    private async Task HandleEvent(EventModel eventModel, ContextEventData<CommandRequest> _)
    {
        switch (eventModel.EventType)
        {
            case EventType.ADD:
            case EventType.UPDATE:
                await UpdatedEvent();
                break;
            case EventType.DELETE: break;
        }
    }

    private async Task UpdatedEvent()
    {
        await _hub.Clients.All.ReceiveRequestUpdate();
    }
}
