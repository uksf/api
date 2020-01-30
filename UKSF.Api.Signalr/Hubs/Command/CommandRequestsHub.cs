using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Hubs;

namespace UKSF.Api.Signalr.Hubs.Command {
    [Authorize]
    public class CommandRequestsHub : Hub<ICommandRequestsClient> {
        public const string END_POINT = "commandRequests";
    }
}
