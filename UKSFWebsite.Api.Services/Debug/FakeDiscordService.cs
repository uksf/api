using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Debug {
    public class FakeDiscordService : DiscordService {
        public FakeDiscordService(IConfiguration configuration, IRanksService ranksService, IUnitsService unitsService, IAccountService accountService, IDisplayNameService displayNameService) : base(configuration, ranksService, unitsService, accountService, displayNameService) { }

        public override Task SendMessage(ulong channelId, string message) => Task.CompletedTask;

        public override Task UpdateAllUsers() => Task.CompletedTask;

        public override Task UpdateAccount(Account account, ulong discordId = 0) => Task.CompletedTask;
    }
}
