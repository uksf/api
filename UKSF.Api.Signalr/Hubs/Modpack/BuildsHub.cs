using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using UKSF.Api.Interfaces.Hubs;

namespace UKSF.Api.Signalr.Hubs.Modpack {
    [Authorize]
    public class BuildsHub : Hub<IModpackClient> {
        public const string END_POINT = "builds";

        public override async Task OnConnectedAsync() {
            StringValues buildId = Context.GetHttpContext().Request.Query["buildId"];
            if (!string.IsNullOrEmpty(buildId)) {
                await Groups.AddToGroupAsync(Context.ConnectionId, buildId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception) {
            StringValues buildId = Context.GetHttpContext().Request.Query["buildId"];
            if (!string.IsNullOrEmpty(buildId)) {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, buildId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
