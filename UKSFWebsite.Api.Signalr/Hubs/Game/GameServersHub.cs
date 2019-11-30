using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using UKSFWebsite.Api.Interfaces.Hubs;

namespace UKSFWebsite.Api.Signalr.Hubs.Game {
    public class GameServersHub : Hub<IGameServersClient> {
        public const string END_POINT = "gameservers";
        public const string ALL = "all";

        public override async Task OnConnectedAsync() {
            StringValues key = Context.GetHttpContext().Request.Query["key"];
            await Groups.AddToGroupAsync(Context.ConnectionId, key == ALL ? ALL : key.ToString());
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception) {
            StringValues key = Context.GetHttpContext().Request.Query["key"];
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, key == ALL ? ALL : key.ToString());
            await base.OnDisconnectedAsync(exception);
        }
    }
}
