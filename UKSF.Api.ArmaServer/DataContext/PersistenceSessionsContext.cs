using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.Core.Context.Base;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IPersistenceSessionsContext : IMongoContext<DomainPersistenceSession>;

public class PersistenceSessionsContext(IMongoCollectionFactory mongoCollectionFactory)
    : MongoContextBase<DomainPersistenceSession>(mongoCollectionFactory, "persistenceSessions"), IPersistenceSessionsContext;
