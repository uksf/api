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
using UKSF.Api.Shared.Signalr.Clients;
using UKSF.Api.Shared.Signalr.Hubs;

namespace UKSF.Api.Personnel.EventHandlers
{
    public interface IAccountDataEventHandler : IEventHandler { }

    public class AccountDataEventHandler : IAccountDataEventHandler
    {
        private readonly IHubContext<AllHub, IAllClient> _allHub;
        private readonly IEventBus _eventBus;
        private readonly IHubContext<AccountGroupedHub, IAccountGroupedClient> _groupedHub;
        private readonly IHubContext<AccountHub, IAccountClient> _hub;
        private readonly ILogger _logger;

        public AccountDataEventHandler(
            IEventBus eventBus,
            IHubContext<AccountHub, IAccountClient> hub,
            IHubContext<AccountGroupedHub, IAccountGroupedClient> groupedHub,
            IHubContext<AllHub, IAllClient> allHub,
            ILogger logger
        )
        {
            _eventBus = eventBus;
            _hub = hub;
            _groupedHub = groupedHub;
            _allHub = allHub;
            _logger = logger;
        }

        public void EarlyInit() { }

        public void Init()
        {
            _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainAccount>>(HandleAccountEvent, _logger.LogError);
            _eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainUnit>>(HandleUnitEvent, _logger.LogError);
        }

        private async Task HandleAccountEvent(EventModel eventModel, ContextEventData<DomainAccount> contextEventData)
        {
            if (eventModel.EventType == EventType.UPDATE)
            {
                await UpdatedEvent(contextEventData.Id);
            }
        }

        private async Task HandleUnitEvent(EventModel eventModel, ContextEventData<DomainUnit> contextEventData)
        {
            if (eventModel.EventType == EventType.UPDATE)
            {
                await UpdatedEvent(contextEventData.Id);
            }
        }

        private async Task UpdatedEvent(string id)
        {
            var oldTask = _hub.Clients.Group(id).ReceiveAccountUpdate();
            var groupedTask = _groupedHub.Clients.Group(id).ReceiveAccountUpdate();
            var allTask = _allHub.Clients.All.ReceiveAccountUpdate();

            await Task.WhenAll(oldTask, groupedTask, allTask);
        }
    }
}
