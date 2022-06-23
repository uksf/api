using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Teamspeak.Signalr.Clients;

namespace UKSF.Api.Teamspeak.Signalr.Hubs
{
    public class TeamspeakClientsHub : Hub<ITeamspeakClientsClient>
    {
        public const string EndPoint = "teamspeakClients";
    }
}
