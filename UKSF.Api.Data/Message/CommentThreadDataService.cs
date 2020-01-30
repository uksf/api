using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Data.Message {
    public class CommentThreadDataService : CachedDataService<CommentThread, ICommentThreadDataService>, ICommentThreadDataService {
        public CommentThreadDataService(IMongoDatabase database, IDataEventBus<ICommentThreadDataService> dataEventBus) : base(database, dataEventBus, "commentThreads") { }

        public new async Task<string> Add(CommentThread commentThread) {
            await base.Add(commentThread);
            return commentThread.id;
        }
        
        public async Task Update(string id, Comment comment, DataEventType updateType) {
            await base.Update(id, updateType == DataEventType.ADD ? Builders<CommentThread>.Update.Push("comments", comment) : Builders<CommentThread>.Update.Pull("comments", comment));
            CommentThreadDataEvent(EventModelFactory.CreateDataEvent<ICommentThreadDataService>(updateType, id, comment));
        }

        private void CommentThreadDataEvent(DataEventModel<ICommentThreadDataService> dataEvent) {
            base.CachedDataEvent(dataEvent);
        }
        
        protected override void CachedDataEvent(DataEventModel<ICommentThreadDataService> dataEvent) { }
    }
}
