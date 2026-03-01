using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IMissionStatsBatchesContext : IMongoContext<MissionStatsBatch>;

public class MissionStatsBatchesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<MissionStatsBatch>(mongoCollectionFactory, eventBus, "missionStatsBatches"), IMissionStatsBatchesContext;
