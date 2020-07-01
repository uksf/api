using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;
using UKSF.Api.Signalr.Hubs.Message;
using UKSF.Common;

namespace UKSF.Api.Events.Handlers {
    public class CommentThreadEventHandler : ICommentThreadEventHandler {
        private readonly ICommentThreadService commentThreadService;
        private readonly ICommentThreadDataService data;
        private readonly IHubContext<CommentThreadHub, ICommentThreadClient> hub;
        private readonly ILoggingService loggingService;

        public CommentThreadEventHandler(
            ICommentThreadDataService data,
            IHubContext<CommentThreadHub, ICommentThreadClient> hub,
            ICommentThreadService commentThreadService,
            ILoggingService loggingService
        ) {
            this.data = data;
            this.hub = hub;
            this.commentThreadService = commentThreadService;
            this.loggingService = loggingService;
        }

        public void Init() {
            data.EventBus().SubscribeAsync(HandleEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleEvent(DataEventModel<ICommentThreadDataService> x) {
            switch (x.type) {
                case DataEventType.ADD:
                    await AddedEvent(x.id, x.data as Comment);
                    break;
                case DataEventType.DELETE:
                    await DeletedEvent(x.id, x.data as Comment);
                    break;
                case DataEventType.UPDATE: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private async Task AddedEvent(string id, Comment comment) {
            await hub.Clients.Group(id).ReceiveComment(commentThreadService.FormatComment(comment));
        }

        private async Task DeletedEvent(string id, DatabaseObject comment) {
            await hub.Clients.Group(id).DeleteComment(comment.id);
        }
    }
}
