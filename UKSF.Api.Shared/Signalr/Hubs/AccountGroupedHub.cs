using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Shared.Signalr.Clients;

namespace UKSF.Api.Shared.Signalr.Hubs
{
    [Authorize]
    public class AccountGroupedHub : Hub<IAccountGroupedClient>
    {
        public const string EndPoint = "accountGrouped";

        public override async Task OnConnectedAsync()
        {
            var userId = Context.GetHttpContext().Request.Query["userId"];
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.GetHttpContext().Request.Query["userId"];
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
