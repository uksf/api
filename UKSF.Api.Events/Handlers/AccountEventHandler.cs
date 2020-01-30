using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Models.Events;
using UKSF.Api.Signalr.Hubs.Personnel;

namespace UKSF.Api.Events.Handlers {
    public class AccountEventHandler : IAccountEventHandler {
        private readonly IAccountDataService accountData;
        private readonly IHubContext<AccountHub, IAccountClient> hub;
        private readonly IUnitsDataService unitsData;

        public AccountEventHandler(IAccountDataService accountData, IUnitsDataService unitsData, IHubContext<AccountHub, IAccountClient> hub) {
            this.accountData = accountData;
            this.unitsData = unitsData;
            this.hub = hub;
        }

        public void Init() {
            accountData.EventBus()
                       .Subscribe(
                           async x => {
                               if (x.type == DataEventType.UPDATE) await UpdatedEvent(x.id);
                           }
                       );
            unitsData.EventBus()
                     .Subscribe(
                         async x => {
                             if (x.type == DataEventType.UPDATE) await UpdatedEvent(x.id);
                         }
                     );
        }

        private async Task UpdatedEvent(string id) {
            await hub.Clients.Group(id).ReceiveAccountUpdate();
        }
    }
}
