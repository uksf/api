using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Events.Handlers;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Services.Hubs;

namespace UKSFWebsite.Api.Events.Handlers {
    public class CommentThreadEventHandler : ICommentThreadEventHandler {
        private readonly IHubContext<CommentThreadHub, ICommentThreadClient> hub;
        private readonly ICommentThreadService commentThreadService;
        private readonly ICommentThreadDataService data;

        public CommentThreadEventHandler(ICommentThreadDataService data, IHubContext<CommentThreadHub, ICommentThreadClient> hub, ICommentThreadService commentThreadService) {
            this.data = data;
            this.hub = hub;
            this.commentThreadService = commentThreadService;
        }

        public void Init() {
            data.EventBus()
                .Subscribe(
                    async x => {
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
                );
        }

        private async Task AddedEvent(string id, Comment comment) {
            await hub.Clients.Group(id).ReceiveComment(commentThreadService.FormatComment(comment));
        }

        private async Task DeletedEvent(string id, Comment comment) {
            await hub.Clients.Group(id).DeleteComment(comment.id);
        }
    }
}
