using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Command.Signalr.Clients;

namespace UKSF.Api.Command.Signalr.Hubs {
    [Authorize]
    public class CommandRequestsHub : Hub<ICommandRequestsClient> {
        public const string END_POINT = "commandRequests";
    }
}
