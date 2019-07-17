using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Hubs {
    [Authorize]
    public class TeamspeakClientsHub : Hub<ITeamspeakClientsClient> {
        public const string END_POINT = "teamspeakClients";
    }
}
