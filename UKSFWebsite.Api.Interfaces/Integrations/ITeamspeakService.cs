using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Integrations {
    public interface ITeamspeakService {
        string GetOnlineTeamspeakClients();
        (bool online, string nickname) GetOnlineUserDetails(Account account);
        object GetFormattedClients();
        Task UpdateClients(string newClientsString);
        void UpdateAccountTeamspeakGroups(Account account);
        void SendTeamspeakMessageToClient(Account account, string message);
        void SendTeamspeakMessageToClient(IEnumerable<string> clientDbIds, string message);
        void Shutdown();
        Task StoreTeamspeakServerSnapshot();
    }
}
