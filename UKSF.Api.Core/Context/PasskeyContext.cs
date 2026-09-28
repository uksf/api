using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Context;

public interface IPasskeyContext : IMongoContext<DomainPasskey>;

public class PasskeyContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainPasskey>(mongoCollectionFactory, eventBus, "passkeys"), IPasskeyContext;
