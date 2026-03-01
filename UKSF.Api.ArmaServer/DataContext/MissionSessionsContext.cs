using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IMissionSessionsContext : IMongoContext<MissionSession>;

public class MissionSessionsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<MissionSession>(mongoCollectionFactory, eventBus, "missionSessions"), IMissionSessionsContext;
