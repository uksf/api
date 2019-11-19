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
        void UpdateAccountTeamspeakGroups(Account account);
        void SendTeamspeakMessageToClient(Account account, string message);
        void SendTeamspeakMessageToClient(IEnumerable<double> clientDbIds, string message);
        void Shutdown();
        Task StoreTeamspeakServerSnapshot();
    }
}
