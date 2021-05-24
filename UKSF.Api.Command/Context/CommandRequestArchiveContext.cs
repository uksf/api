using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Command.Context
{
    public interface ICommandRequestArchiveContext : IMongoContext<CommandRequest> { }

    public class CommandRequestArchiveContext : MongoContext<CommandRequest>, ICommandRequestArchiveContext
    {
        public CommandRequestArchiveContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "commandRequestsArchive") { }

        protected override void DataEvent(EventModel eventModel) { }
    }
}
