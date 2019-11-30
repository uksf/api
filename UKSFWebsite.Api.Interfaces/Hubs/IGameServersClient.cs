using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Game;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface IGameServersClient {
        Task ReceiveServersUpdate(List<GameServer> servers);
        Task ReceiveServerAdded(GameServer server);
        Task ReceiveServerUpdate(GameServer server);
        Task ReceiveServerRemoved(string key);
        Task ReceiveDisabledState(bool state);
        Task ReceiveServerStatusUpdate(GameServerStatus status);
        Task ReceiveServerStatusesUpdate(List<GameServerStatus> statuses);
        Task ReceiveServerStatusRemoved(string key);
        Task ReceiveServerStatusesCleared();
    }
}
