// using System.Threading.Tasks;
// using Microsoft.Extensions.Configuration;
//
// namespace UKSF.Api.Fake {
//     public class FakeDiscordService : DiscordService {
//         public FakeDiscordService(
//             IConfiguration configuration,
//             IRanksService ranksService,
//             IUnitsService unitsService,
//             IAccountService accountService,
//             IDisplayNameService displayNameService,
//             IVariablesService variablesService
//         ) : base(configuration, ranksService, unitsService, accountService, displayNameService, variablesService) { }
//
//         public override Task SendMessageToEveryone(ulong channelId, string message) => Task.CompletedTask;
//
//         public override Task SendMessage(ulong channelId, string message) => Task.CompletedTask;
//
//         public override Task UpdateAllUsers() => Task.CompletedTask;
//
//         public override Task UpdateAccount(Account account, ulong discordId = 0) => Task.CompletedTask;
//     }
// }


