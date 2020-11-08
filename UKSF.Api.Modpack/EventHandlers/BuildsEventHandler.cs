using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Models;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Signalr.Clients;
using UKSF.Api.Modpack.Signalr.Hubs;

namespace UKSF.Api.Modpack.EventHandlers {
    public interface IBuildsEventHandler : IEventHandler { }

    public class BuildsEventHandler : IBuildsEventHandler {
        private readonly IDataEventBus<ModpackBuild> modpackBuildEventBus;
        private readonly IHubContext<BuildsHub, IModpackClient> hub;
        private readonly ILogger logger;

        public BuildsEventHandler(IDataEventBus<ModpackBuild> modpackBuildEventBus, IHubContext<BuildsHub, IModpackClient> hub, ILogger logger) {
            this.modpackBuildEventBus = modpackBuildEventBus;
            this.hub = hub;
            this.logger = logger;
        }

        public void Init() {
            modpackBuildEventBus.AsObservable().SubscribeWithAsyncNext(HandleBuildEvent, exception => logger.LogError(exception));
        }

        private async Task HandleBuildEvent(DataEventModel<ModpackBuild> dataEventModel) {
            if (dataEventModel.data == null) return;

            switch (dataEventModel.type) {
                case DataEventType.ADD:
                    await AddedEvent(dataEventModel.data as ModpackBuild);
                    break;
                case DataEventType.UPDATE:
                    await UpdatedEvent(dataEventModel.id, dataEventModel.data);
                    break;
                case DataEventType.DELETE: break;
                default: throw new ArgumentOutOfRangeException(nameof(dataEventModel));
            }
        }

        private async Task AddedEvent(ModpackBuild build) {
            if (build.environment == GameEnvironment.DEV) {
                await hub.Clients.All.ReceiveBuild(build);
            } else {
                await hub.Clients.All.ReceiveReleaseCandidateBuild(build);
            }
        }

        private async Task UpdatedEvent(string id, object data) {
            switch (data) {
                case ModpackBuild build:
                    if (build.environment == GameEnvironment.DEV) {
                        await hub.Clients.All.ReceiveBuild(build);
                    } else {
                        await hub.Clients.All.ReceiveReleaseCandidateBuild(build);
                    }

                    break;
                case ModpackBuildStep step:
                    await hub.Clients.Group(id).ReceiveBuildStep(step);
                    break;
            }
        }
    }
}
