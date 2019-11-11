using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Interfaces.Data.Cached {
    public interface ICommentThreadDataService : IDataService<CommentThread> {
        new Task<string> Add(CommentThread commentThread);
    }
}
