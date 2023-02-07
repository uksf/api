using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core.Signalr.Clients;

namespace UKSF.Api.Core.Signalr.Hubs;

[Authorize]
public class AccountHub : Hub<IAccountClient>
{
    public const string EndPoint = "account";

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
