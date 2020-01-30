using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Hubs {
    public interface ITeamspeakClientsClient {
        Task ReceiveClients(object clients);
    }
}
