using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Personnel.EventHandlers
{
    public interface IAccountDataEventHandler : IEventHandler { }

    public class AccountDataEventHandler : IAccountDataEventHandler
    {
        private readonly IEventBus _eventBus;
        private readonly IHubContext<AccountHub, IAccountClient> _hub;
        private readonly ILogger _logger;

        public AccountDataEventHandler(IEventBus eventBus, IHubContext<AccountHub, IAccountClient> hub, ILogger logger)
        {
            _eventBus = eventBus;
            _hub = hub;
            _logger = logger;
        }

        public void Init()
        {
            _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainAccount>>(HandleAccountEvent, _logger.LogError);
            _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<Unit>>(HandleUnitEvent, _logger.LogError);
        }

        private async Task HandleAccountEvent(EventModel eventModel, ContextEventData<DomainAccount> contextEventData)
        {
            if (eventModel.EventType == EventType.UPDATE)
            {
                await UpdatedEvent(contextEventData.Id);
            }
        }

        private async Task HandleUnitEvent(EventModel eventModel, ContextEventData<Unit> contextEventData)
        {
            if (eventModel.EventType == EventType.UPDATE)
            {
                await UpdatedEvent(contextEventData.Id);
            }
        }

        private async Task UpdatedEvent(string id)
        {
            await _hub.Clients.Group(id).ReceiveAccountUpdate();
        }
    }
}
