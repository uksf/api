using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Signalr.Clients;
using UKSF.Api.Core.Signalr.Hubs;

namespace UKSF.Api.EventHandlers;

public interface IAccountDataEventHandler : IEventHandler { }

public class AccountDataEventHandler : IAccountDataEventHandler
{
    private readonly IHubContext<AllHub, IAllClient> _allHub;
    private readonly IEventBus _eventBus;
    private readonly IHubContext<AccountGroupedHub, IAccountGroupedClient> _groupedHub;
    private readonly IHubContext<AccountHub, IAccountClient> _hub;
    private readonly IUksfLogger _logger;

    public AccountDataEventHandler(
        IEventBus eventBus,
        IHubContext<AccountHub, IAccountClient> hub,
        IHubContext<AccountGroupedHub, IAccountGroupedClient> groupedHub,
        IHubContext<AllHub, IAllClient> allHub,
        IUksfLogger logger
    )
    {
        _eventBus = eventBus;
        _hub = hub;
        _groupedHub = groupedHub;
        _allHub = allHub;
        _logger = logger;
    }

    public void EarlyInit() { }

    public void Init()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainAccount>>(HandleAccountEvent, _logger.LogError);
        _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainUnit>>(HandleUnitEvent, _logger.LogError);
    }

    private async Task HandleAccountEvent(EventModel eventModel, ContextEventData<DomainAccount> contextEventData)
    {
        if (eventModel.EventType == EventType.UPDATE)
        {
            await UpdatedEvent(contextEventData.Id);
        }
    }

    private async Task HandleUnitEvent(EventModel eventModel, ContextEventData<DomainUnit> contextEventData)
    {
        if (eventModel.EventType == EventType.UPDATE)
        {
            await UpdatedEvent(contextEventData.Id);
        }
    }

    private async Task UpdatedEvent(string id)
    {
        var oldTask = _hub.Clients.Group(id).ReceiveAccountUpdate();
        var groupedTask = _groupedHub.Clients.Group(id).ReceiveAccountUpdate();
        var allTask = _allHub.Clients.All.ReceiveAccountUpdate();

        await Task.WhenAll(oldTask, groupedTask, allTask);
    }
}
