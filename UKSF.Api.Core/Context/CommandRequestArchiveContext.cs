using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

public interface ICommandRequestArchiveContext : IMongoContext<CommandRequest> { }

public class CommandRequestArchiveContext : MongoContext<CommandRequest>, ICommandRequestArchiveContext
{
    public CommandRequestArchiveContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(
        mongoCollectionFactory,
        eventBus,
        "commandRequestsArchive"
    ) { }

    protected override void DataEvent(EventModel eventModel) { }
}
