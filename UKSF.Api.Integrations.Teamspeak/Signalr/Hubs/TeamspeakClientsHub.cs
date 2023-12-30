using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Integrations.Teamspeak.Signalr.Clients;

namespace UKSF.Api.Integrations.Teamspeak.Signalr.Hubs;

public class TeamspeakClientsHub : Hub<ITeamspeakClientsClient>
{
    public const string EndPoint = "teamspeakClients";
}
