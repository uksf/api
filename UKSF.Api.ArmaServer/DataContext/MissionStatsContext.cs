using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IMissionStatsContext : IMongoContext<MissionStats>;

public class MissionStatsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<MissionStats>(mongoCollectionFactory, eventBus, "missionStats"), IMissionStatsContext;
