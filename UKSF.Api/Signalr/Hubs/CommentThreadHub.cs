using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Signalr.Clients;

namespace UKSF.Api.Signalr.Hubs;

[Authorize]
public class CommentThreadHub : Hub<ICommentThreadClient>
{
    public const string EndPoint = "commentThread";

    public override async Task OnConnectedAsync()
    {
        var threadId = Context.GetHttpContext().Request.Query["threadId"];
        await Groups.AddToGroupAsync(Context.ConnectionId, threadId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var threadId = Context.GetHttpContext().Request.Query["threadId"];
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, threadId);
        await base.OnDisconnectedAsync(exception);
    }
}
