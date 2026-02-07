using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Signalr.Clients;

namespace UKSF.Api.Signalr.Hubs;

// Intentionally not [Authorize] - provides unauthenticated updates (e.g. connection status) to the frontend
public class UtilityHub : Hub<IUtilityClient>
{
    public const string EndPoint = "utility";
}
