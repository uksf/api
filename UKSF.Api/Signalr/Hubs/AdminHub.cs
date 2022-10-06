using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Signalr.Clients;

namespace UKSF.Api.Signalr.Hubs;

[Authorize]
public class AdminHub : Hub<IAdminClient>
{
    public const string EndPoint = "admin";
}
