using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Personnel {
    public class AccountService : IAccountService {
        private readonly IHubContext<AccountHub, IAccountClient> accountHub;
        private readonly IAccountDataService data;

        public AccountService(IAccountDataService data, IHubContext<AccountHub, IAccountClient> accountHub) {
            this.data = data;
            this.accountHub = accountHub;
        }

        public IAccountDataService Data() => data;

        public async Task Update(string id, string fieldName, object value) {
            await data.Update(id, fieldName, value);
            await accountHub.Clients.Group(id).ReceiveAccountUpdate(); // TODO: Implement event bus for signals, move to base data services
        }

        public async Task Update(string id, UpdateDefinition<Account> update) {
            await data.Update(id, update);
            await accountHub.Clients.Group(id).ReceiveAccountUpdate();
        }
    }
}
