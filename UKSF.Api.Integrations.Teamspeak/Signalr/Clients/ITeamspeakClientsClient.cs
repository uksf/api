using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Teamspeak.Models;

namespace UKSF.Api.Teamspeak.Signalr.Clients
{
    public interface ITeamspeakClientsClient
    {
        Task ReceiveClients(List<TeamspeakClient> clients);
    }
}
