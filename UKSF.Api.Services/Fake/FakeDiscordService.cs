using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Integrations;

namespace UKSF.Api.Services.Fake {
    public class FakeDiscordService : DiscordService {
        public FakeDiscordService(IConfiguration configuration, IRanksService ranksService, IUnitsService unitsService, IAccountService accountService, IDisplayNameService displayNameService) : base(configuration, ranksService, unitsService, accountService, displayNameService) { }

        public override Task SendMessage(ulong channelId, string message) => Task.CompletedTask;

        public override Task UpdateAllUsers() => Task.CompletedTask;

        public override Task UpdateAccount(Account account, ulong discordId = 0) => Task.CompletedTask;
    }
}
