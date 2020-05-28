using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Signalr.Hubs.Personnel;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class AccountEventHandler : IAccountEventHandler {
        private readonly IAccountDataService accountData;
        private readonly IHubContext<AccountHub, IAccountClient> hub;
        private readonly ILoggingService loggingService;
        private readonly IUnitsDataService unitsData;

        public AccountEventHandler(IAccountDataService accountData, IUnitsDataService unitsData, IHubContext<AccountHub, IAccountClient> hub, ILoggingService loggingService) {
            this.accountData = accountData;
            this.unitsData = unitsData;
            this.hub = hub;
            this.loggingService = loggingService;
        }

        public void Init() {
            accountData.EventBus().SubscribeAsync(HandleAccountsEvent, exception => loggingService.Log(exception));
            unitsData.EventBus().SubscribeAsync(HandleUnitsEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleAccountsEvent(DataEventModel<IAccountDataService> x) {
            if (x.type == DataEventType.UPDATE) {
                await UpdatedEvent(x.id);
            }
        }

        private async Task HandleUnitsEvent(DataEventModel<IUnitsDataService> x) {
            if (x.type == DataEventType.UPDATE) {
                await UpdatedEvent(x.id);
            }
        }

        private async Task UpdatedEvent(string id) {
            await hub.Clients.Group(id).ReceiveAccountUpdate();
        }
    }
}
