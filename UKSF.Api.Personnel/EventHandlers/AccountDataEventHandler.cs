using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Personnel.EventHandlers {
    public interface IAccountDataEventHandler : IEventHandler { }

    public class AccountDataEventHandler : IAccountDataEventHandler {
        private readonly IDataEventBus<Account> _accountDataEventBus;
        private readonly IHubContext<AccountHub, IAccountClient> _hub;
        private readonly ILogger _logger;
        private readonly IDataEventBus<Unit> _unitDataEventBus;

        public AccountDataEventHandler(IDataEventBus<Account> accountDataEventBus, IDataEventBus<Unit> unitDataEventBus, IHubContext<AccountHub, IAccountClient> hub, ILogger logger) {
            _accountDataEventBus = accountDataEventBus;
            _unitDataEventBus = unitDataEventBus;
            _hub = hub;
            _logger = logger;
        }

        public void Init() {
            _accountDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleAccountsEvent, exception => _logger.LogError(exception));
            _unitDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleUnitsEvent, exception => _logger.LogError(exception));
        }

        private async Task HandleAccountsEvent(DataEventModel<Account> dataEventModel) {
            if (dataEventModel.type == DataEventType.UPDATE) {
                await UpdatedEvent(dataEventModel.id);
            }
        }

        private async Task HandleUnitsEvent(DataEventModel<Unit> dataEventModel) {
            if (dataEventModel.type == DataEventType.UPDATE) {
                await UpdatedEvent(dataEventModel.id);
            }
        }

        private async Task UpdatedEvent(string id) {
            await _hub.Clients.Group(id).ReceiveAccountUpdate();
        }
    }
}
