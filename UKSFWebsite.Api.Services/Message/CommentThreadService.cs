using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Services.Message {
    public class CommentThreadService : DataBackedService<ICommentThreadDataService>, ICommentThreadService {
        private readonly IDisplayNameService displayNameService;

        public CommentThreadService(ICommentThreadDataService data, IDisplayNameService displayNameService) : base(data) => this.displayNameService = displayNameService;

        public IEnumerable<Comment> GetCommentThreadComments(string id) => Data().GetSingle(id).comments.Reverse();

        public async Task InsertComment(string id, Comment comment) {
            await Data().Update(id, comment, DataEventType.ADD);
        }

        public async Task RemoveComment(string id, Comment comment) {
            await Data().Update(id, comment, DataEventType.DELETE);
        }

        public IEnumerable<string> GetCommentThreadParticipants(string id) {
            HashSet<string> participants = GetCommentThreadComments(id).Select(x => x.author).ToHashSet();
            participants.UnionWith(Data().GetSingle(id).authors);
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
