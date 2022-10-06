using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Signalr.Clients;

namespace UKSF.Api.Signalr.Hubs;

[Authorize]
public class CommandRequestsHub : Hub<ICommandRequestsClient>
{
    public const string EndPoint = "commandRequests";
}
