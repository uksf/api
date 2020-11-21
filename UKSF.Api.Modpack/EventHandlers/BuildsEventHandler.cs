using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Events;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Signalr.Clients;
using UKSF.Api.Modpack.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Modpack.EventHandlers {
    public interface IBuildsEventHandler : IEventHandler { }

    public class BuildsEventHandler : IBuildsEventHandler {
        private readonly IHubContext<BuildsHub, IModpackClient> _hub;
        private readonly ILogger _logger;
        private readonly IDataEventBus<ModpackBuild> _modpackBuildEventBus;

        public BuildsEventHandler(IDataEventBus<ModpackBuild> modpackBuildEventBus, IHubContext<BuildsHub, IModpackClient> hub, ILogger logger) {
            _modpackBuildEventBus = modpackBuildEventBus;
            _hub = hub;
            _logger = logger;
        }

        public void Init() {
            _modpackBuildEventBus.AsObservable().SubscribeWithAsyncNext(HandleBuildEvent, exception => _logger.LogError(exception));
        }

        private async Task HandleBuildEvent(DataEventModel<ModpackBuild> dataEventModel) {
            if (dataEventModel.Data == null) return;

            switch (dataEventModel.Type) {
                case DataEventType.ADD:
                    await AddedEvent(dataEventModel.Data as ModpackBuild);
                    break;
                case DataEventType.UPDATE:
                    await UpdatedEvent(dataEventModel.Id, dataEventModel.Data);
                    break;
                case DataEventType.DELETE: break;
                default:                   throw new ArgumentOutOfRangeException(nameof(dataEventModel));
            }
        }

        private async Task AddedEvent(ModpackBuild build) {
            if (build.Environment == GameEnvironment.DEV) {
                await _hub.Clients.All.ReceiveBuild(build);
            } else {
                await _hub.Clients.All.ReceiveReleaseCandidateBuild(build);
            }
        }

        private async Task UpdatedEvent(string id, object data) {
            switch (data) {
                case ModpackBuild build:
                    if (build.Environment == GameEnvironment.DEV) {
                        await _hub.Clients.All.ReceiveBuild(build);
                    } else {
                        await _hub.Clients.All.ReceiveReleaseCandidateBuild(build);
                    }

                    break;
                case ModpackBuildStep step:
                    await _hub.Clients.Group(id).ReceiveBuildStep(step);
                    break;
            }
        }
    }
}
