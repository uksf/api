using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Personnel.EventHandlers {
    public interface ICommentThreadEventHandler : IEventHandler { }

    public class CommentThreadEventHandler : ICommentThreadEventHandler {
        private readonly IDataEventBus<CommentThread> commentThreadDataEventBus;
        private readonly ICommentThreadService commentThreadService;
        private readonly IHubContext<CommentThreadHub, ICommentThreadClient> hub;
        private readonly ILogger logger;

        public CommentThreadEventHandler(
            IDataEventBus<CommentThread> commentThreadDataEventBus,
            IHubContext<CommentThreadHub, ICommentThreadClient> hub,
            ICommentThreadService commentThreadService,
            ILogger logger
        ) {
            this.commentThreadDataEventBus = commentThreadDataEventBus;
            this.hub = hub;
            this.commentThreadService = commentThreadService;
            this.logger = logger;
        }

        public void Init() {
            commentThreadDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => logger.LogError(exception));
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

        private Task AddedEvent(string id, Comment comment) => hub.Clients.Group(id).ReceiveComment(commentThreadService.FormatComment(comment));

        private Task DeletedEvent(string id, DatabaseObject comment) => hub.Clients.Group(id).DeleteComment(comment.id);
    }
}
