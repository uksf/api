using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Personnel.Services {
    public interface ICommentThreadService {
        IEnumerable<Comment> GetCommentThreadComments(string id);
        Task InsertComment(string id, Comment comment);
        Task RemoveComment(string id, Comment comment);
        IEnumerable<string> GetCommentThreadParticipants(string id);
        object FormatComment(Comment comment);
    }

    public class CommentThreadService : ICommentThreadService {
        private readonly ICommentThreadContext _commentThreadContext;
        private readonly IDisplayNameService _displayNameService;

        public CommentThreadService(ICommentThreadContext commentThreadContext, IDisplayNameService displayNameService) {
            _commentThreadContext = commentThreadContext;
            _displayNameService = displayNameService;
        }

        public IEnumerable<Comment> GetCommentThreadComments(string id) => _commentThreadContext.GetSingle(id).Comments.Reverse();

        public async Task InsertComment(string id, Comment comment) {
            await _commentThreadContext.Update(id, comment, DataEventType.ADD);
        }

        public async Task RemoveComment(string id, Comment comment) {
            await _commentThreadContext.Update(id, comment, DataEventType.DELETE);
        }

        public IEnumerable<string> GetCommentThreadParticipants(string id) {
            HashSet<string> participants = GetCommentThreadComments(id).Select(x => x.Author).ToHashSet();
            participants.UnionWith(_commentThreadContext.GetSingle(id).Authors);
            return participants;
        }

        public object FormatComment(Comment comment) => new { comment.Id, comment.Author, comment.Content, DisplayName = _displayNameService.GetDisplayName(comment.Author), comment.Timestamp };
    }
}
