﻿using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Signalr.Clients;
using UKSF.Api.Modpack.Signalr.Hubs;

namespace UKSF.Api.Modpack.EventHandlers;

public interface IBuildsEventHandler : IEventHandler { }

public class BuildsEventHandler : IBuildsEventHandler
{
    private readonly IEventBus _eventBus;
    private readonly IHubContext<BuildsHub, IModpackClient> _hub;
    private readonly IUksfLogger _logger;

    public BuildsEventHandler(IEventBus eventBus, IHubContext<BuildsHub, IModpackClient> hub, IUksfLogger logger)
    {
        _eventBus = eventBus;
        _hub = hub;
        _logger = logger;
    }

    public void EarlyInit() { }

    public void Init()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<ModpackBuild>(HandleBuildEvent, _logger.LogError);
        _eventBus.AsObservable().SubscribeWithAsyncNext<ModpackBuildStepEventData>(HandleBuildStepEvent, _logger.LogError);
    }

    private async Task HandleBuildStepEvent(EventModel eventModel, ModpackBuildStepEventData data)
    {
        if (data.BuildStep == null)
        {
            return;
        }

        if (eventModel.EventType == EventType.UPDATE)
        {
            await _hub.Clients.Group(data.BuildId).ReceiveBuildStep(data.BuildStep);
        }
    }

    private async Task HandleBuildEvent(EventModel eventModel, ModpackBuild build)
    {
        if (build == null)
        {
            return;
        }

        switch (eventModel.EventType)
        {
            case EventType.ADD:
                await AddedEvent(build);
                break;
            case EventType.UPDATE:
                await UpdatedEvent(build);
                break;
        }
    }

    private async Task AddedEvent(ModpackBuild build)
    {
        if (build.Environment == GameEnvironment.DEVELOPMENT)
        {
            await _hub.Clients.All.ReceiveBuild(build);
        }
        else
        {
            await _hub.Clients.All.ReceiveReleaseCandidateBuild(build);
        }
    }

    private async Task UpdatedEvent(ModpackBuild build)
    {
        if (build.Environment == GameEnvironment.DEVELOPMENT)
        {
            await _hub.Clients.All.ReceiveBuild(build);
        }
        else
        {
            await _hub.Clients.All.ReceiveReleaseCandidateBuild(build);
        }
    }
}
