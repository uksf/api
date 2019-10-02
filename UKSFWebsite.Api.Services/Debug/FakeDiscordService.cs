using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Debug {
    public class FakeDiscordService : IDiscordService {
        public Task ConnectDiscord() => Task.CompletedTask;

        public bool IsAccountOnline(Account account) => false;

        public Task SendMessage(ulong channelId, string message) => Task.CompletedTask;

        public Task<IReadOnlyCollection<SocketRole>> GetRoles() => Task.FromResult<IReadOnlyCollection<SocketRole>>(new List<SocketRole>());

        public Task UpdateAllUsers() => Task.CompletedTask;

        public Task UpdateAccount(Account account, ulong discordId = 0) => Task.CompletedTask;
    }
}
