using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Data {
    public class AccountService : CachedDataService<Account>, IAccountService {
        private readonly IHubContext<AccountHub, IAccountClient> accountHub;

        public AccountService(IMongoDatabase database, IHubContext<AccountHub, IAccountClient> accountHub) : base(database, "accounts") => this.accountHub = accountHub;

        public override async Task Update(string id, string fieldName, object value) {
            await base.Update(id, fieldName, value);
            await accountHub.Clients.Group(id).ReceiveAccountUpdate();
        }

        public override async Task Update(string id, UpdateDefinition<Account> update) {
            await base.Update(id, update);
            await accountHub.Clients.Group(id).ReceiveAccountUpdate();
        }
    }
}
