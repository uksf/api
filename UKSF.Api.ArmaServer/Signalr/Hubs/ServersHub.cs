using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Signalr.Clients;

namespace UKSF.Api.ArmaServer.Signalr.Hubs;

[Authorize]
public class ServersHub : Hub<IServersClient>
{
    public const string EndPoint = "servers";
}
