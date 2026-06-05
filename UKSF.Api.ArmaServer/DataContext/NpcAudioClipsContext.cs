using UKSF.Api.ArmaServer.Npc.Models;
using UKSF.Api.Core.Context.Base;

namespace UKSF.Api.ArmaServer.DataContext;

public interface INpcAudioClipsContext : IMongoContext<DomainNpcAudioClip>;

public class NpcAudioClipsContext(IMongoCollectionFactory mongoCollectionFactory)
    : MongoContextBase<DomainNpcAudioClip>(mongoCollectionFactory, "npcAudioClips"), INpcAudioClipsContext;
