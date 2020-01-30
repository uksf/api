using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Hubs;

namespace UKSF.Api.Signalr.Hubs.Integrations {
    public class TeamspeakClientsHub : Hub<ITeamspeakClientsClient> {
        public const string END_POINT = "teamspeakClients";
    }
}
