using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.Signalr.Clients;

namespace UKSF.Api.Admin.Signalr.Hubs
{
    public class UtilityHub : Hub<IUtilityClient>
    {
        public const string EndPoint = "utility";
    }
}
