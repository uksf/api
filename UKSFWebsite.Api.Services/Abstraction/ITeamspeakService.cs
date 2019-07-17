using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Accounts;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ITeamspeakService {
        string GetOnlineTeamspeakClients();
        object GetFormattedClients();
        Task UpdateClients(string newClientsString);
        void UpdateAccountTeamspeakGroups(Account account);
        void SendTeamspeakMessageToClient(Account account, string message);
        void SendTeamspeakMessageToClient(IEnumerable<string> clientDbIds, string message);
        void Shutdown();
        Task StoreTeamspeakServerSnapshot();
    }
}
