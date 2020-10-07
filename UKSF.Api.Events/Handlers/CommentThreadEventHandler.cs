using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Events;
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
        private readonly IDataEventBus<CommentThread> commentThreadDataEventBus;
        private readonly ICommentThreadService commentThreadService;
        private readonly IHubContext<CommentThreadHub, ICommentThreadClient> hub;
        private readonly ILoggingService loggingService;

        public CommentThreadEventHandler(
            IDataEventBus<CommentThread> commentThreadDataEventBus,
            IHubContext<CommentThreadHub, ICommentThreadClient> hub,
            ICommentThreadService commentThreadService,
            ILoggingService loggingService
        ) {
            this.commentThreadDataEventBus = commentThreadDataEventBus;
            this.hub = hub;
            this.commentThreadService = commentThreadService;
            this.loggingService = loggingService;
        }

        public void Init() {
            commentThreadDataEventBus.AsObservable().SubscribeAsync(HandleEvent, exception => loggingService.Log(exception));
        }

        private async Task HandleEvent(DataEventModel<CommentThread> dataEventModel) {
            switch (dataEventModel.type) {
                case DataEventType.ADD:
                    await AddedEvent(dataEventModel.id, dataEventModel.data as Comment);
                    break;
                case DataEventType.DELETE:
                    await DeletedEvent(dataEventModel.id, dataEventModel.data as Comment);
                    break;
                case DataEventType.UPDATE: break;
                default:                   throw new ArgumentOutOfRangeException(nameof(dataEventModel));
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
