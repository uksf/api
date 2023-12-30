using UKSF.Api.Integrations.Teamspeak.Models;

namespace UKSF.Api.Integrations.Teamspeak.Signalr.Clients;

public interface ITeamspeakClientsClient
{
    Task ReceiveClients(List<TeamspeakConnectClient> clients);
}
