using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Personnel.Services {
    public interface ICommentThreadService : IDataBackedService<ICommentThreadDataService> {
        IEnumerable<Comment> GetCommentThreadComments(string id);
        Task InsertComment(string id, Comment comment);
        Task RemoveComment(string id, Comment comment);
        IEnumerable<string> GetCommentThreadParticipants(string id);
        object FormatComment(Comment comment);
    }

    public class CommentThreadService : DataBackedService<ICommentThreadDataService>, ICommentThreadService {
        private readonly IDisplayNameService displayNameService;

        public CommentThreadService(ICommentThreadDataService data, IDisplayNameService displayNameService) : base(data) => this.displayNameService = displayNameService;

        public IEnumerable<Comment> GetCommentThreadComments(string id) => Data.GetSingle(id).comments.Reverse();

        public async Task InsertComment(string id, Comment comment) {
            await Data.Update(id, comment, DataEventType.ADD);
        }

        public async Task RemoveComment(string id, Comment comment) {
            await Data.Update(id, comment, DataEventType.DELETE);
        }

        public IEnumerable<string> GetCommentThreadParticipants(string id) {
            HashSet<string> participants = GetCommentThreadComments(id).Select(x => x.author).ToHashSet();
            participants.UnionWith(Data.GetSingle(id).authors);
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
