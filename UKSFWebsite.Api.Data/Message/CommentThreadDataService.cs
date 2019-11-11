using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Data.Message {
    public class CommentThreadDataService : CachedDataService<CommentThread>, ICommentThreadDataService {
        public CommentThreadDataService(IMongoDatabase database) : base(database, "commentThreads") { }

        public new async Task<string> Add(CommentThread commentThread) {
            await base.Add(commentThread);
            return commentThread.id;
        }
    }
}
