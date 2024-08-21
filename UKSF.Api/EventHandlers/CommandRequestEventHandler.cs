using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.EventHandlers;

public interface ICommandRequestEventHandler : IEventHandler;

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
        _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainCommandRequest>>(HandleEvent, _logger.LogError);
    }

    private async Task HandleEvent(EventModel eventModel, ContextEventData<DomainCommandRequest> _)
    {
        switch (eventModel.EventType)
        {
            case EventType.Add:
            case EventType.Update:
                await UpdatedEvent();
                break;
            case EventType.Delete: break;
        }
    }

    private async Task UpdatedEvent()
    {
        await _hub.Clients.All.ReceiveRequestUpdate();
    }
}
