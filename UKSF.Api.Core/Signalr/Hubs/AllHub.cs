using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core.Signalr.Clients;

namespace UKSF.Api.Core.Signalr.Hubs;

[Authorize]
public class AllHub : Hub<IAllClient>
{
    public const string EndPoint = "all";
}
