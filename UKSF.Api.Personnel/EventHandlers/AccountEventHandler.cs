using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;

namespace UKSF.Api.Personnel.EventHandlers {
    public interface IAccountEventHandler : IEventHandler { }

    public class AccountEventHandler : IAccountEventHandler {
        private readonly IDataEventBus<Account> accountDataEventBus;
        private readonly IHubContext<AccountHub, IAccountClient> hub;
        private readonly ILogger logger;
        private readonly IDataEventBus<Unit> unitDataEventBus;

        public AccountEventHandler(IDataEventBus<Account> accountDataEventBus, IDataEventBus<Unit> unitDataEventBus, IHubContext<AccountHub, IAccountClient> hub, ILogger logger) {
            this.accountDataEventBus = accountDataEventBus;
            this.unitDataEventBus = unitDataEventBus;
            this.hub = hub;
            this.logger = logger;
        }

        public void Init() {
            accountDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleAccountsEvent, exception => logger.LogError(exception));
            unitDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleUnitsEvent, exception => logger.LogError(exception));
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
            await hub.Clients.Group(id).ReceiveAccountUpdate();
        }
    }
}
