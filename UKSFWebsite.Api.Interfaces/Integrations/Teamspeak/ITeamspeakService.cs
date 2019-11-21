using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Integrations;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakService {
        HashSet<TeamspeakClient> GetOnlineTeamspeakClients();
        (bool online, string nickname) GetOnlineUserDetails(Account account);
        object GetFormattedClients();
        Task UpdateClients(HashSet<TeamspeakClient> newClients);
        Task UpdateAccountTeamspeakGroups(Account account);
        Task SendTeamspeakMessageToClient(Account account, string message);
        Task SendTeamspeakMessageToClient(IEnumerable<double> clientDbIds, string message);
        Task Shutdown();
        Task StoreTeamspeakServerSnapshot();
    }
}
