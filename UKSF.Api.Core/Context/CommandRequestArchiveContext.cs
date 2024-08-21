using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Context;

public interface ICommandRequestArchiveContext : IMongoContext<DomainCommandRequest>;

public class CommandRequestArchiveContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainCommandRequest>(mongoCollectionFactory, eventBus, "commandRequestsArchive"), ICommandRequestArchiveContext;
