using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Signalr.Clients;

namespace UKSF.Api.Signalr.Hubs;

[Authorize]
public class BoardHub : Hub<IBoardClient>
{
    public const string EndPoint = "board";

    public override async Task OnConnectedAsync()
    {
        var boardId = Context.GetHttpContext().Request.Query["boardId"];
        await Groups.AddToGroupAsync(Context.ConnectionId, boardId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var boardId = Context.GetHttpContext().Request.Query["boardId"];
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, boardId);
        await base.OnDisconnectedAsync(exception);
    }
}
