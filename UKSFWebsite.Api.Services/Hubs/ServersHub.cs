using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Services.Hubs {
    public class ServersHub : Hub<IServersClient> {
        public const string END_POINT = "servers";
    }
}
