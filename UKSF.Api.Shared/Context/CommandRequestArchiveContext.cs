using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

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
