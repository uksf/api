using System.Threading.Tasks;

namespace UKSF.Api.Teamspeak.Signalr.Clients {
    public interface ITeamspeakClientsClient {
        Task ReceiveClients(object clients);
    }
}
