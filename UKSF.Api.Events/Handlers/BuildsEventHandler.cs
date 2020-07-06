using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Signalr.Hubs.Modpack;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class BuildsEventHandler : IBuildsEventHandler {
        private readonly IBuildsDataService data;
        private readonly IHubContext<BuildsHub, IModpackClient> hub;
        private readonly ILoggingService loggingService;

        public BuildsEventHandler(IBuildsDataService data, IHubContext<BuildsHub, IModpackClient> hub, ILoggingService loggingService) {
            this.data = data;
            this.hub = hub;
            this.loggingService = loggingService;
        }

        public void Init() {
            data.EventBus().SubscribeAsync(HandleEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleEvent(DataEventModel<IBuildsDataService> x) {
            switch (x.type) {
                case DataEventType.ADD:
                    await AddedEvent(x.id, x.data);
                    break;
                case DataEventType.UPDATE:
                    await UpdatedEvent(x.id, x.data);
                    break;
                case DataEventType.DELETE: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private async Task AddedEvent(string version, object dataObject) {
            switch (dataObject) {
                case ModpackBuild build:
                    await hub.Clients.All.ReceiveBuild(version, build);
                    break;
                case ModpackBuildRelease buildRelease:
                    await hub.Clients.All.ReceiveBuildRelease(buildRelease);
                    break;
            }
        }

        private async Task UpdatedEvent(string version, object dataObject) {
            switch (dataObject) {
                case ModpackBuild build:
                    await hub.Clients.All.ReceiveBuild(version, build);
                    break;
                case ModpackBuildStep step:
                    await hub.Clients.Group(version).ReceiveBuildStep(step);
                    break;
            }
        }
    }
}
