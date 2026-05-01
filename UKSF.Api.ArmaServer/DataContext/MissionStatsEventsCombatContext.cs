using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IMissionStatsEventsCombatContext : IMongoContext<MissionStatsEventsCombat>;

public class MissionStatsEventsCombatContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<MissionStatsEventsCombat>(mongoCollectionFactory, eventBus, "missionStatsEventsCombat"), IMissionStatsEventsCombatContext;
