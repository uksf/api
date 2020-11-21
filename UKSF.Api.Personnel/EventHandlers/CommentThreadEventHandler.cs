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
        private readonly IDataEventBus<CommentThread> _commentThreadDataEventBus;
        private readonly ICommentThreadService _commentThreadService;
        private readonly IHubContext<CommentThreadHub, ICommentThreadClient> _hub;
        private readonly ILogger _logger;

        public CommentThreadEventHandler(
            IDataEventBus<CommentThread> commentThreadDataEventBus,
            IHubContext<CommentThreadHub, ICommentThreadClient> hub,
            ICommentThreadService commentThreadService,
            ILogger logger
        ) {
            _commentThreadDataEventBus = commentThreadDataEventBus;
            _hub = hub;
            _commentThreadService = commentThreadService;
            _logger = logger;
        }

        public void Init() {
            _commentThreadDataEventBus.AsObservable().SubscribeWithAsyncNext(HandleEvent, exception => _logger.LogError(exception));
        }

        private async Task HandleEvent(DataEventModel<CommentThread> dataEventModel) {
            switch (dataEventModel.Type) {
                case DataEventType.ADD:
                    await AddedEvent(dataEventModel.Id, dataEventModel.Data as Comment);
                    break;
                case DataEventType.DELETE:
                    await DeletedEvent(dataEventModel.Id, dataEventModel.Data as Comment);
                    break;
                case DataEventType.UPDATE: break;
                default:                   throw new ArgumentOutOfRangeException(nameof(dataEventModel));
            }
        }

        private Task AddedEvent(string id, Comment comment) => _hub.Clients.Group(id).ReceiveComment(_commentThreadService.FormatComment(comment));

        private Task DeletedEvent(string id, MongoObject comment) => _hub.Clients.Group(id).DeleteComment(comment.Id);
    }
}
