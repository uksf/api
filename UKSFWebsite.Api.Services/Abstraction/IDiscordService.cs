using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using UKSFWebsite.Api.Models.Accounts;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IDiscordService {
        Task ConnectDiscord();
        Task SendMessage(ulong channelId, string message);
        Task<IReadOnlyCollection<SocketRole>> GetRoles();
        Task UpdateAllUsers();
        Task UpdateAccount(Account account, ulong discordId = 0);
    }
}