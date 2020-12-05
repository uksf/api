using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Signalr.Clients;
using UKSF.Api.Modpack.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Modpack.EventHandlers {
    public interface IBuildsEventHandler : IEventHandler { }

    public class BuildsEventHandler : IBuildsEventHandler {
        private readonly IEventBus _eventBus;
        private readonly IHubContext<BuildsHub, IModpackClient> _hub;
        private readonly ILogger _logger;

        public BuildsEventHandler(IEventBus eventBus, IHubContext<BuildsHub, IModpackClient> hub, ILogger logger) {
            _eventBus = eventBus;
            _hub = hub;
            _logger = logger;
        }

        public void Init() {
            _eventBus.AsObservable().SubscribeWithAsyncNext<ModpackBuild>(HandleBuildEvent, _logger.LogError);
            _eventBus.AsObservable().SubscribeWithAsyncNext<ModpackBuildStepEventData>(HandleBuildStepEvent, _logger.LogError);
        }

        private async Task HandleBuildStepEvent(EventModel eventModel, ModpackBuildStepEventData data) {
            if (data.BuildStep == null) return;
            if (eventModel.EventType == EventType.UPDATE) {
                await _hub.Clients.Group(data.BuildId).ReceiveBuildStep(data.BuildStep);
            }
        }

        private async Task HandleBuildEvent(EventModel eventModel, ModpackBuild build) {
            if (build == null) return;

            switch (eventModel.EventType) {
                case EventType.ADD:
                    await AddedEvent(build);
                    break;
                case EventType.UPDATE:
                    await UpdatedEvent(build);
                    break;
            }
        }

        private async Task AddedEvent(ModpackBuild build) {
            if (build.Environment == GameEnvironment.DEV) {
                await _hub.Clients.All.ReceiveBuild(build);
            } else {
                await _hub.Clients.All.ReceiveReleaseCandidateBuild(build);
            }
        }

        private async Task UpdatedEvent(ModpackBuild build) {
            if (build.Environment == GameEnvironment.DEV) {
                await _hub.Clients.All.ReceiveBuild(build);
            } else {
                await _hub.Clients.All.ReceiveReleaseCandidateBuild(build);
            }
        }
    }
}
