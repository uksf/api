using System.Threading.Tasks;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface ICommentThreadDataService : IDataService<CommentThread, ICommentThreadDataService> {
        new Task<string> Add(CommentThread commentThread);
        Task Update(string id, Comment comment, DataEventType updateType);
    }
}
