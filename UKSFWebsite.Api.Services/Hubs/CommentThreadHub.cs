using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using UKSFWebsite.Api.Services.Hubs.Abstraction;

namespace UKSFWebsite.Api.Services.Hubs {
    [Authorize]
    public class CommentThreadHub : Hub<ICommentThreadClient> {
        public const string END_POINT = "commentThread";

        public override async Task OnConnectedAsync() {
            StringValues threadId = Context.GetHttpContext().Request.Query["threadId"];
            await Groups.AddToGroupAsync(Context.ConnectionId, threadId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception) {
            StringValues threadId = Context.GetHttpContext().Request.Query["threadId"];
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, threadId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
