using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Events.Handlers;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Services.Hubs;

namespace UKSFWebsite.Api.Events.Handlers {
    public class CommandRequestEventHandler : ICommandRequestEventHandler {
        private readonly ICommandRequestDataService data;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> hub;

        public CommandRequestEventHandler(ICommandRequestDataService data, IHubContext<CommandRequestsHub, ICommandRequestsClient> hub) {
            this.data = data;
            this.hub = hub;
        }

        public void Init() {
            data.EventBus()
                .Subscribe(
                    async x => {
                        switch (x.type) {
                            case DataEventType.ADD:
                            case DataEventType.UPDATE:
                                await UpdatedEvent();
                                break;
                            case DataEventType.DELETE: break;
                            default: throw new ArgumentOutOfRangeException();
                        }
                    }
                );
        }

        private async Task UpdatedEvent() {
            await hub.Clients.All.ReceiveRequestUpdate();
        }
    }
}
