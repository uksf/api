using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IMissionSessionsContext : IMongoContext<MissionSession>, ICachedMongoContext;

public class MissionSessionsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<MissionSession>(mongoCollectionFactory, eventBus, variablesService, "missionSessions"), IMissionSessionsContext;
