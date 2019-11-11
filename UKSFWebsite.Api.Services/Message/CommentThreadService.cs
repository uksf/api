using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Services.Message {
    public class CommentThreadService : ICommentThreadService {
        private readonly ICommentThreadDataService data;

        public CommentThreadService(ICommentThreadDataService data) => this.data = data;

        public ICommentThreadDataService Data() => data;

        public IEnumerable<Comment> GetCommentThreadComments(string id) => data.GetSingle(id).comments.Reverse();

        public async Task InsertComment(string id, Comment comment) {
            await data.Update(id, Builders<CommentThread>.Update.Push("comments", comment));
        }

        public async Task RemoveComment(string id, Comment comment) {
            await data.Update(id, Builders<CommentThread>.Update.Pull("comments", comment));
        }

        public IEnumerable<string> GetCommentThreadParticipants(string id) {
            HashSet<string> participants = GetCommentThreadComments(id).Select(x => x.author).ToHashSet();
            participants.UnionWith(data.GetSingle(id).authors);
            return participants;
        }
    }
}
