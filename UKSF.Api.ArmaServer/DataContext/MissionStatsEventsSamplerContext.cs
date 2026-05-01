using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IMissionStatsEventsSamplerContext : IMongoContext<MissionStatsEventsSampler>;

public class MissionStatsEventsSamplerContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<MissionStatsEventsSampler>(mongoCollectionFactory, eventBus, "missionStatsEventsSampler"), IMissionStatsEventsSamplerContext;
