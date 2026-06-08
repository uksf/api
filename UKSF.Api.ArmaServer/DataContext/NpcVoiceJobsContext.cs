using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core.Context.Base;

namespace UKSF.Api.ArmaServer.DataContext;

public interface INpcVoiceJobsContext : IMongoContext<DomainNpcVoiceJob>;

public class NpcVoiceJobsContext(IMongoCollectionFactory mongoCollectionFactory)
    : MongoContextBase<DomainNpcVoiceJob>(mongoCollectionFactory, "npcVoiceJobs"), INpcVoiceJobsContext;
