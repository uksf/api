using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Signalr.Clients;
using UKSF.Api.Command.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Command.EventHandlers
{
    public interface ICommandRequestEventHandler : IEventHandler { }

    public class CommandRequestEventHandler : ICommandRequestEventHandler
    {
        private readonly IEventBus _eventBus;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> _hub;
        private readonly ILogger _logger;

        public CommandRequestEventHandler(IEventBus eventBus, IHubContext<CommandRequestsHub, ICommandRequestsClient> hub, ILogger logger)
        {
            _eventBus = eventBus;
            _hub = hub;
            _logger = logger;
        }

        public void EarlyInit() { }

        public void Init()
        {
            _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<CommandRequest>>(HandleEvent, _logger.LogError);
        }

        private async Task HandleEvent(EventModel eventModel, ContextEventData<CommandRequest> _)
        {
            switch (eventModel.EventType)
            {
                case EventType.ADD:
                case EventType.UPDATE:
                    await UpdatedEvent();
                    break;
                case EventType.DELETE: break;
            }
        }

        private async Task UpdatedEvent()
        {
            await _hub.Clients.All.ReceiveRequestUpdate();
        }
    }
}
