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
        private readonly IDataEventBus<CommandRequest> _commandRequestDataEventBus;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> _hub;
        private readonly ILogger _logger;

        public CommandRequestEventHandler(IDataEventBus<CommandRequest> commandRequestDataEventBus, IHubContext<CommandRequestsHub, ICommandRequestsClient> hub, ILogger logger) {
            _commandRequestDataEventBus = commandRequestDataEventBus;
            _hub = hub;
            _logger = logger;
        }

        public void Init() {
            _commandRequestDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => _logger.LogError(exception));
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
            await _hub.Clients.All.ReceiveRequestUpdate();
        }
    }
}
