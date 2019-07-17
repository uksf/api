using System.Threading.Tasks;

namespace UKSFWebsite.Api.Services.Hubs.Abstraction {
    public interface ITeamspeakClientsClient {
        Task ReceiveClients(object clients);
    }
}
