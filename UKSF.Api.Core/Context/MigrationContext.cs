using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

public interface IMigrationContext : IMongoContext<Migration>;

public class MigrationContext : MongoContext<Migration>, IMigrationContext
{
    public MigrationContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "migrations") { }
}
