using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Signalr.Hubs.Integrations {
    public class TeamspeakClientsHub : Hub<ITeamspeakClientsClient> {
        public const string END_POINT = "teamspeakClients";
    }
}
