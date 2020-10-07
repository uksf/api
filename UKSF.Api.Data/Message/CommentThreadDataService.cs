using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Data.Message {
    public class CommentThreadDataService : CachedDataService<CommentThread>, ICommentThreadDataService {
        public CommentThreadDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<CommentThread> dataEventBus) : base(dataCollectionFactory, dataEventBus, "commentThreads") { }

        public async Task Update(string id, Comment comment, DataEventType updateType) {
            await base.Update(id, updateType == DataEventType.ADD ? Builders<CommentThread>.Update.Push(x => x.comments, comment) : Builders<CommentThread>.Update.Pull(x => x.comments, comment));
            CommentThreadDataEvent(EventModelFactory.CreateDataEvent<CommentThread>(updateType, id, comment));
        }

        private void CommentThreadDataEvent(DataEventModel<CommentThread> dataEvent) {
            base.DataEvent(dataEvent);
        }

        protected override void DataEvent(DataEventModel<CommentThread> dataEvent) { }
    }
}
