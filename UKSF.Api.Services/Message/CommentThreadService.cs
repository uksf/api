using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Services.Message {
    public class CommentThreadService : ICommentThreadService {
        private readonly ICommentThreadDataService data;
        private readonly IDisplayNameService displayNameService;

        public CommentThreadService(ICommentThreadDataService data, IDisplayNameService displayNameService) {
            this.data = data;
            this.displayNameService = displayNameService;
        }

        public ICommentThreadDataService Data() => data;

        public IEnumerable<Comment> GetCommentThreadComments(string id) => data.GetSingle(id).comments.Reverse();

        public async Task InsertComment(string id, Comment comment) {
            await data.Update(id, comment, DataEventType.ADD);
        }

        public async Task RemoveComment(string id, Comment comment) {
            await data.Update(id, comment, DataEventType.DELETE);
        }

        public IEnumerable<string> GetCommentThreadParticipants(string id) {
            HashSet<string> participants = GetCommentThreadComments(id).Select(x => x.author).ToHashSet();
            participants.UnionWith(data.GetSingle(id).authors);
            return participants;
        }

        public object FormatComment(Comment comment) =>
            new {
                Id = comment.id,
                Author = comment.author,
                Content = comment.content,
                DisplayName = displayNameService.GetDisplayName(comment.author),
                Timestamp = comment.timestamp
            };
    }
}
