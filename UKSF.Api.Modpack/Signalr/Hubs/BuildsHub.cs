using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Modpack.Signalr.Clients;

namespace UKSF.Api.Modpack.Signalr.Hubs
{
    [Authorize]
    public class BuildsHub : Hub<IModpackClient>
    {
        public const string EndPoint = "builds";

        public override async Task OnConnectedAsync()
        {
            var buildId = Context.GetHttpContext().Request.Query["buildId"];
            if (!string.IsNullOrEmpty(buildId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, buildId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var buildId = Context.GetHttpContext().Request.Query["buildId"];
            if (!string.IsNullOrEmpty(buildId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, buildId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
