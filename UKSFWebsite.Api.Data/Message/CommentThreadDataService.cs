using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Events.Data;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Data.Message {
    public class CommentThreadDataService : CachedDataService<CommentThread>, ICommentThreadDataService {
        public CommentThreadDataService(IMongoDatabase database, IDataEventBus dataEventBus) : base(database, dataEventBus, "commentThreads") { }

        public new async Task<string> Add(CommentThread commentThread) {
            await base.Add(commentThread);
            return commentThread.id;
        }
        
        public async Task Update(string id, Comment comment, DataEventType updateType) {
            await base.Update(id, updateType == DataEventType.ADD ? Builders<CommentThread>.Update.Push("comments", comment) : Builders<CommentThread>.Update.Pull("comments", comment));
            CommentThreadDataEvent(EventModelFactory.CreateDataEvent(updateType, id, comment));
        }

        private void CommentThreadDataEvent(DataEventModel dataEvent) {
            base.CachedDataEvent(dataEvent);
        }
        
        protected override void CachedDataEvent(DataEventModel dataEvent) { }
    }
}
