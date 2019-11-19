using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Signalr.Hubs.Game {
    public class ServersHub : Hub<IServersClient> {
        public const string END_POINT = "servers";
    }
}
