using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Models;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Signalr.Clients;
using UKSF.Api.Command.Signalr.Hubs;

namespace UKSF.Api.Command.EventHandlers {
    public interface ICommandRequestEventHandler : IEventHandler { }

    public class CommandRequestEventHandler : ICommandRequestEventHandler {
        private readonly IDataEventBus<CommandRequest> commandRequestDataEventBus;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> hub;
        private readonly ILogger logger;

        public CommandRequestEventHandler(IDataEventBus<CommandRequest> commandRequestDataEventBus, IHubContext<CommandRequestsHub, ICommandRequestsClient> hub, ILogger logger) {
            this.commandRequestDataEventBus = commandRequestDataEventBus;
            this.hub = hub;
            this.logger = logger;
        }

        public void Init() {
            commandRequestDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => logger.LogError(exception));
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
