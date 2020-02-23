using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Events;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;
using UKSF.Common;

namespace UKSF.Api.Data.Message {
    public class CommentThreadDataService : CachedDataService<CommentThread, ICommentThreadDataService>, ICommentThreadDataService {
        public CommentThreadDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ICommentThreadDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "commentThreads") { }

        public async Task Update(string id, Comment comment, DataEventType updateType) {
            await base.Update(id, updateType == DataEventType.ADD ? Builders<CommentThread>.Update.Push(x => x.comments, comment) : Builders<CommentThread>.Update.Pull(x => x.comments, comment));
            CommentThreadDataEvent(EventModelFactory.CreateDataEvent<ICommentThreadDataService>(updateType, id, comment));
        }

        private void CommentThreadDataEvent(DataEventModel<ICommentThreadDataService> dataEvent) {
            base.CachedDataEvent(dataEvent);
        }

        protected override void CachedDataEvent(DataEventModel<ICommentThreadDataService> dataEvent) { }
    }
}
