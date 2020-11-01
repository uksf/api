using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services.Data {
    public interface ICommentThreadDataService : IDataService<CommentThread>, ICachedDataService {
        Task Update(string id, Comment comment, DataEventType updateType);
    }

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
