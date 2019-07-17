using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Data {
    public class CommentThreadService : CachedDataService<CommentThread>, ICommentThreadService {
        public CommentThreadService(IMongoDatabase database) : base(database, "commentThreads") { }

        public Comment[] GetCommentThreadComments(string id) => GetSingle(id).comments.Reverse().ToArray();

        public new async Task<string> Add(CommentThread commentThread) {
            await base.Add(commentThread);
            return commentThread.id;
        }

        public async Task InsertComment(string id, Comment comment) {
            await Update(id, Builders<CommentThread>.Update.Push("comments", comment));
        }

        public async Task RemoveComment(string id, Comment comment) {
            await Update(id, Builders<CommentThread>.Update.Pull("comments", comment));
        }

        public IEnumerable<string> GetCommentThreadParticipants(string id) {
            HashSet<string> participants = GetCommentThreadComments(id).Select(x => x.author).ToHashSet();
            participants.UnionWith(GetSingle(id).authors);
            return participants;
        }
    }
}
