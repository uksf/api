using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Signalr.Clients;
using UKSF.Api.Modpack.Signalr.Hubs;

namespace UKSF.Api.Modpack.EventHandlers;

public interface IWorkshopModDataEventHandler : IEventHandler;

public class WorkshopModDataEventHandler(IEventBus eventBus, IHubContext<ModpackHub, IModpackClient> hub, IUksfLogger logger) : IWorkshopModDataEventHandler
{
    public void EarlyInit() { }

    public void Init()
    {
        eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainWorkshopMod>>(HandleEvent, logger.LogError);
    }

    private async Task HandleEvent(EventModel eventModel, ContextEventData<DomainWorkshopMod> eventData)
    {
        switch (eventModel.EventType)
        {
            case EventType.Add:
            case EventType.Delete:
                // Both add and delete require a full mod list refresh on the frontend
                await ModListChangedEvent(); break;
            case EventType.Update: await UpdatedEvent(eventData.Id); break;
        }
    }

    private async Task ModListChangedEvent()
    {
        await hub.Clients.All.ReceiveWorkshopModAdded();
    }

    private async Task UpdatedEvent(string id)
    {
        await hub.Clients.All.ReceiveWorkshopModUpdate(id);
    }
}
