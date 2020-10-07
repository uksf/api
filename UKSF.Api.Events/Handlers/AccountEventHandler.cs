using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Units;
using UKSF.Api.Signalr.Hubs.Personnel;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class AccountEventHandler : IAccountEventHandler {
        private readonly IDataEventBus<Account> accountDataEventBus;
        private readonly IHubContext<AccountHub, IAccountClient> hub;
        private readonly ILoggingService loggingService;
        private readonly IDataEventBus<Unit> unitDataEventBus;

        public AccountEventHandler(IDataEventBus<Account> accountDataEventBus, IDataEventBus<Unit> unitDataEventBus, IHubContext<AccountHub, IAccountClient> hub, ILoggingService loggingService) {
            this.accountDataEventBus = accountDataEventBus;
            this.unitDataEventBus = unitDataEventBus;
            this.hub = hub;
            this.loggingService = loggingService;
        }

        public void Init() {
            accountDataEventBus.AsObservable().SubscribeAsync(HandleAccountsEvent, exception => loggingService.Log(exception));
            unitDataEventBus.AsObservable().SubscribeAsync(HandleUnitsEvent, exception => loggingService.Log(exception));
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
