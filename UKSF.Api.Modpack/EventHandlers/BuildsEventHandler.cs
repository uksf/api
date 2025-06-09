using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Signalr.Clients;
using UKSF.Api.Modpack.Signalr.Hubs;

namespace UKSF.Api.Modpack.EventHandlers;

public interface IBuildsEventHandler : IEventHandler;

public class BuildsEventHandler(IEventBus eventBus, IHubContext<ModpackHub, IModpackClient> hub, IUksfLogger logger) : IBuildsEventHandler
{
    public void EarlyInit() { }

    public void Init()
    {
        eventBus.AsObservable().SubscribeWithAsyncNext<ModpackBuildEventData>(HandleBuildEvent, logger.LogError);
        eventBus.AsObservable().SubscribeWithAsyncNext<ModpackBuildStepEventData>(HandleBuildStepEvent, logger.LogError);
    }

    private async Task HandleBuildStepEvent(EventModel eventModel, ModpackBuildStepEventData data)
    {
        if (data.BuildStep == null)
        {
            return;
        }

        if (eventModel.EventType == EventType.Update)
        {
            await hub.Clients.Group(data.BuildId).ReceiveBuildStep(data.BuildStep);
        }
    }

    private async Task HandleBuildEvent(EventModel eventModel, ModpackBuildEventData data)
    {
        if (data.Build == null)
        {
            return;
        }

        switch (eventModel.EventType)
        {
            case EventType.Add:    await AddedEvent(data.Build); break;
            case EventType.Update: await UpdatedEvent(data.Build); break;
        }
    }

    private async Task AddedEvent(DomainModpackBuild build)
    {
        if (build.Environment == GameEnvironment.Development)
        {
            await hub.Clients.All.ReceiveBuild(build);
        }
        else
        {
            await hub.Clients.All.ReceiveReleaseCandidateBuild(build);
        }
    }

    private async Task UpdatedEvent(DomainModpackBuild build)
    {
        if (build.Environment == GameEnvironment.Development)
        {
            await hub.Clients.All.ReceiveBuild(build);
        }
        else
        {
            await hub.Clients.All.ReceiveReleaseCandidateBuild(build);
        }
    }
}
