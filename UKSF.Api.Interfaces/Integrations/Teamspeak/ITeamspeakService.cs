using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Models.Integrations;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakService {
        IEnumerable<TeamspeakClient> GetOnlineTeamspeakClients();
        (bool online, string nickname) GetOnlineUserDetails(Account account);
        IEnumerable<object> GetFormattedClients();
        Task UpdateClients(HashSet<TeamspeakClient> newClients);
        Task UpdateAccountTeamspeakGroups(Account account);
        Task SendTeamspeakMessageToClient(Account account, string message);
        Task SendTeamspeakMessageToClient(IEnumerable<double> clientDbIds, string message);
        Task Shutdown();
        Task StoreTeamspeakServerSnapshot();
    }
}
