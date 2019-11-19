using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Signalr.Hubs.Command {
    [Authorize]
    public class CommandRequestsHub : Hub<ICommandRequestsClient> {
        public const string END_POINT = "commandRequests";
    }
}
