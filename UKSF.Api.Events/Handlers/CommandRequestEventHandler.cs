using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Events;
using UKSF.Api.Signalr.Hubs.Command;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class CommandRequestEventHandler : ICommandRequestEventHandler {
        private readonly IDataEventBus<CommandRequest> commandRequestDataEventBus;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> hub;
        private readonly ILoggingService loggingService;

        public CommandRequestEventHandler(IDataEventBus<CommandRequest> commandRequestDataEventBus, IHubContext<CommandRequestsHub, ICommandRequestsClient> hub, ILoggingService loggingService) {
            this.commandRequestDataEventBus = commandRequestDataEventBus;
            this.hub = hub;
            this.loggingService = loggingService;
        }

        public void Init() {
            commandRequestDataEventBus.AsObservable().SubscribeAsync(HandleEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleEvent(DataEventModel<CommandRequest> dataEventModel) {
            switch (dataEventModel.type) {
                case DataEventType.ADD:
                case DataEventType.UPDATE:
                    await UpdatedEvent();
                    break;
                case DataEventType.DELETE: break;
                default:                   throw new ArgumentOutOfRangeException(nameof(dataEventModel));
            }
        }

        private async Task UpdatedEvent() {
            await hub.Clients.All.ReceiveRequestUpdate();
        }
    }
}
