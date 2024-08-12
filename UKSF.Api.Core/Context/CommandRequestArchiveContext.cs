using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

public interface ICommandRequestArchiveContext : IMongoContext<CommandRequest>;

public class CommandRequestArchiveContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<CommandRequest>(mongoCollectionFactory, eventBus, "commandRequestsArchive"), ICommandRequestArchiveContext;
