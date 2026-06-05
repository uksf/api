using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core.Context.Base;

namespace UKSF.Api.ArmaServer.DataContext;

public interface INpcSessionsContext : IMongoContext<DomainNpcSession>;

public class NpcSessionsContext(IMongoCollectionFactory mongoCollectionFactory)
    : MongoContextBase<DomainNpcSession>(mongoCollectionFactory, "npcSessions"), INpcSessionsContext;
