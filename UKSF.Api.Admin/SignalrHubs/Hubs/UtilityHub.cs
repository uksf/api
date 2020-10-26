using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.SignalrHubs.Clients;

namespace UKSF.Api.Admin.SignalrHubs.Hubs {
    public class UtilityHub : Hub<IUtilityClient> {
        public const string END_POINT = "utility";
    }
}
