using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IPlayerMissionStatsContext : IMongoContext<PlayerMissionStats>;

public class PlayerMissionStatsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<PlayerMissionStats>(mongoCollectionFactory, eventBus, "playerMissionStats"), IPlayerMissionStatsContext;
