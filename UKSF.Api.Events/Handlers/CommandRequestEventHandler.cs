using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Signalr.Hubs.Command;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class CommandRequestEventHandler : ICommandRequestEventHandler {
        private readonly ICommandRequestDataService data;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> hub;
        private readonly ILoggingService loggingService;

        public CommandRequestEventHandler(ICommandRequestDataService data, IHubContext<CommandRequestsHub, ICommandRequestsClient> hub, ILoggingService loggingService) {
            this.data = data;
            this.hub = hub;
            this.loggingService = loggingService;
        }

        public void Init() {
            data.EventBus().SubscribeAsync(HandleEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleEvent(DataEventModel<ICommandRequestDataService> x) {
            switch (x.type) {
                case DataEventType.ADD:
                case DataEventType.UPDATE:
                    await UpdatedEvent();
                    break;
                case DataEventType.DELETE: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private async Task UpdatedEvent() {
            await hub.Clients.All.ReceiveRequestUpdate();
        }
    }
}
