using System.Threading.Tasks;

namespace UKSFWebsite.Api.Interfaces.Hubs {
    public interface ITeamspeakClientsClient {
        Task ReceiveClients(object clients);
    }
}
