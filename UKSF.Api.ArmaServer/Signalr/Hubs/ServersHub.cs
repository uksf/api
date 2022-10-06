using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.Signalr.Clients;

namespace UKSF.Api.ArmaServer.Signalr.Hubs;

public class ServersHub : Hub<IServersClient>
{
    public const string EndPoint = "servers";
}
