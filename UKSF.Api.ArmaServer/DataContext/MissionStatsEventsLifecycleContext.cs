using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IMissionStatsEventsLifecycleContext : IMongoContext<MissionStatsEventsLifecycle>;

public class MissionStatsEventsLifecycleContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<MissionStatsEventsLifecycle>(mongoCollectionFactory, eventBus, "missionStatsEventsLifecycle"), IMissionStatsEventsLifecycleContext;
