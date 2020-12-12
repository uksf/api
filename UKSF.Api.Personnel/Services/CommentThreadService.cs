using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services {
    public interface ICommentThreadService {
        IEnumerable<Comment> GetCommentThreadComments(string commentThreadId);
        Task InsertComment(string commentThreadId, Comment comment);
        Task RemoveComment(string commentThreadId, Comment comment);
        IEnumerable<string> GetCommentThreadParticipants(string commentThreadId);
        object FormatComment(Comment comment);
    }

    public class CommentThreadService : ICommentThreadService {
        private readonly ICommentThreadContext _commentThreadContext;
        private readonly IDisplayNameService _displayNameService;

        public CommentThreadService(ICommentThreadContext commentThreadContext, IDisplayNameService displayNameService) {
            _commentThreadContext = commentThreadContext;
            _displayNameService = displayNameService;
        }

        public IEnumerable<Comment> GetCommentThreadComments(string commentThreadId) => _commentThreadContext.GetSingle(commentThreadId).Comments.Reverse();

        public async Task InsertComment(string commentThreadId, Comment comment) {
            await _commentThreadContext.AddCommentToThread(commentThreadId, comment);
        }

        public async Task RemoveComment(string commentThreadId, Comment comment) {
            await _commentThreadContext.RemoveCommentFromThread(commentThreadId, comment);
        }

        public IEnumerable<string> GetCommentThreadParticipants(string commentThreadId) {
            HashSet<string> participants = GetCommentThreadComments(commentThreadId).Select(x => x.Author).ToHashSet();
            participants.UnionWith(_commentThreadContext.GetSingle(commentThreadId).Authors);
            return participants;
        }

        public object FormatComment(Comment comment) => new { comment.Id, comment.Author, comment.Content, DisplayName = _displayNameService.GetDisplayName(comment.Author), comment.Timestamp };
    }
}
