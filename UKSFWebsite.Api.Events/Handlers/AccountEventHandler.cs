using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Events.Handlers;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Services.Hubs;

namespace UKSFWebsite.Api.Events.Handlers {
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
