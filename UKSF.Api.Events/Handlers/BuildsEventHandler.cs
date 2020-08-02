using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Signalr.Hubs.Modpack;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class BuildsEventHandler : IBuildsEventHandler {
        private readonly IBuildsDataService buildsData;
        private readonly IHubContext<BuildsHub, IModpackClient> hub;
        private readonly ILoggingService loggingService;

        public BuildsEventHandler(IBuildsDataService buildsData, IHubContext<BuildsHub, IModpackClient> hub, ILoggingService loggingService) {
            this.buildsData = buildsData;
            this.hub = hub;
            this.loggingService = loggingService;
        }

        public void Init() {
            buildsData.EventBus().SubscribeAsync(HandleBuildEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleBuildEvent(DataEventModel<IBuildsDataService> x) {
            if (x.data == null) return;

            switch (x.type) {
                case DataEventType.ADD:
                    await AddedEvent(x.data as ModpackBuild);
                    break;
                case DataEventType.UPDATE:
                    await UpdatedEvent(x.id, x.data);
                    break;
                case DataEventType.DELETE: break;
                default: throw new ArgumentOutOfRangeException();
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
