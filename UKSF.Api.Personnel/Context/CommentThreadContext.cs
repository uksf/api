using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Personnel.Context {
    public interface ICommentThreadContext : IMongoContext<CommentThread>, ICachedMongoContext {
        Task Update(string id, Comment comment, DataEventType updateType);
    }

    public class CommentThreadContext : CachedMongoContext<CommentThread>, ICommentThreadContext {
        public CommentThreadContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<CommentThread> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "commentThreads") { }

        public async Task Update(string id, Comment comment, DataEventType updateType) {
            await base.Update(id, updateType == DataEventType.ADD ? Builders<CommentThread>.Update.Push(x => x.Comments, comment) : Builders<CommentThread>.Update.Pull(x => x.Comments, comment));
            CommentThreadDataEvent(EventModelFactory.CreateDataEvent<CommentThread>(updateType, id, comment));
        }

        private void CommentThreadDataEvent(DataEventModel<CommentThread> dataEvent) {
            base.DataEvent(dataEvent);
        }

        protected override void DataEvent(DataEventModel<CommentThread> dataEvent) { }
    }
}
