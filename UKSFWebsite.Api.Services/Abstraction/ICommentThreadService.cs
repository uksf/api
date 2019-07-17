using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ICommentThreadService : IDataService<CommentThread> {
        new Task<string> Add(CommentThread commentThread);
        Comment[] GetCommentThreadComments(string id);
        Task InsertComment(string id, Comment comment);
        Task RemoveComment(string id, Comment comment);
        IEnumerable<string> GetCommentThreadParticipants(string id);
    }
}
