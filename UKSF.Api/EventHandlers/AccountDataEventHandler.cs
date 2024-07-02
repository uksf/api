using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Signalr.Clients;
using UKSF.Api.Core.Signalr.Hubs;

namespace UKSF.Api.EventHandlers;

public interface IAccountDataEventHandler : IEventHandler;

public class AccountDataEventHandler(
    IEventBus eventBus,
    IHubContext<AccountHub, IAccountClient> accountHub,
    IHubContext<AccountGroupedHub, IAccountGroupedClient> groupedHub,
    IHubContext<AllHub, IAllClient> allHub,
    IUksfLogger logger
) : IAccountDataEventHandler
{
    public void EarlyInit() { }

    public void Init()
    {
        eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainAccount>>(HandleAccountEvent, logger.LogError);
        eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainUnit>>(HandleUnitEvent, logger.LogError);
    }

    private async Task HandleAccountEvent(EventModel eventModel, ContextEventData<DomainAccount> contextEventData)
    {
        if (eventModel.EventType == EventType.Update)
        {
            await UpdatedEvent(contextEventData.Id);
        }
    }

    private async Task HandleUnitEvent(EventModel eventModel, ContextEventData<DomainUnit> contextEventData)
    {
        if (eventModel.EventType == EventType.Update)
        {
            await UpdatedEvent(contextEventData.Id);
        }
    }

    private async Task UpdatedEvent(string id)
    {
        var accountTask = accountHub.Clients.Group(id).ReceiveAccountUpdate();
        var groupedTask = groupedHub.Clients.Group(id).ReceiveAccountUpdate();
        var allTask = allHub.Clients.All.ReceiveAccountUpdate();

        await Task.WhenAll(accountTask, groupedTask, allTask);
    }
}
