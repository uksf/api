using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Shared.Signalr.Clients;

namespace UKSF.Api.Shared.Signalr.Hubs
{
    [Authorize]
    public class AllHub : Hub<IAllClient>
    {
        public const string EndPoint = "all";
    }
}
