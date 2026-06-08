using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core.Context.Base;

namespace UKSF.Api.ArmaServer.DataContext;

public interface INpcVoicesContext : IMongoContext<DomainNpcVoice>;

public class NpcVoicesContext(IMongoCollectionFactory mongoCollectionFactory)
    : MongoContextBase<DomainNpcVoice>(mongoCollectionFactory, "npcVoices"), INpcVoicesContext;
