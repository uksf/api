using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Integrations;

namespace UKSFWebsite.Api.Services.Fake {
    public class FakeDiscordService : DiscordService {
        public FakeDiscordService(IConfiguration configuration, IRanksService ranksService, IUnitsService unitsService, IAccountService accountService, IDisplayNameService displayNameService) : base(configuration, ranksService, unitsService, accountService, displayNameService) { }

        public override Task SendMessage(ulong channelId, string message) => Task.CompletedTask;

        public override Task UpdateAllUsers() => Task.CompletedTask;

        public override Task UpdateAccount(Account account, ulong discordId = 0) => Task.CompletedTask;
    }
}
