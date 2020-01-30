using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Hubs;

namespace UKSF.Api.Signalr.Hubs.Game {
    public class ServersHub : Hub<IServersClient> {
        public const string END_POINT = "servers";
    }
}
