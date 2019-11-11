using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Interfaces.Message {
    public interface ICommentThreadService : IDataBackedService<ICommentThreadDataService> {
        IEnumerable<Comment> GetCommentThreadComments(string id);
        Task InsertComment(string id, Comment comment);
        Task RemoveComment(string id, Comment comment);
        IEnumerable<string> GetCommentThreadParticipants(string id);
        object FormatComment(Comment comment);
    }
}
