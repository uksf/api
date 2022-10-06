using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Signalr.Clients;

namespace UKSF.Api.Signalr.Hubs;

public class UtilityHub : Hub<IUtilityClient>
{
    public const string EndPoint = "utility";
}
