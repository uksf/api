using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface IMigrationContext : IMongoContext<Migration> { }

public class MigrationContext : MongoContext<Migration>, IMigrationContext
{
    public MigrationContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "migrations") { }
}
