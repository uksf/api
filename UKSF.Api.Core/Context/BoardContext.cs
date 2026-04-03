using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Context;

public interface IBoardContext : IMongoContext<DomainBoard> { }

public class BoardContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainBoard>(mongoCollectionFactory, eventBus, "boards"), IBoardContext;
