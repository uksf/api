using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Signalr.Hubs.Personnel {
    [Authorize]
    public class AccountHub : Hub<IAccountClient> {
        public const string END_POINT = "account";

        public override async Task OnConnectedAsync() {
            StringValues userId = Context.GetHttpContext().Request.Query["userId"];
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception) {
            StringValues userId = Context.GetHttpContext().Request.Query["userId"];
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}