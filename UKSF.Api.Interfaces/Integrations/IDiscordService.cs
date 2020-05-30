using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Integrations {
    public interface IDiscordService {
        Task ConnectDiscord();
        (bool online, string nickname) GetOnlineUserDetails(Account account);
        Task SendMessage(ulong channelId, string message);
        Task<IReadOnlyCollection<SocketRole>> GetRoles();
        Task UpdateAllUsers();
        Task UpdateAccount(Account account, ulong discordId = 0);
    }
}
