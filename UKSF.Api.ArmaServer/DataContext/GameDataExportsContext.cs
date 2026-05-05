using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IGameDataExportsContext : IMongoContext<DomainGameDataExport>;

public class GameDataExportsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainGameDataExport>(mongoCollectionFactory, eventBus, "gameDataExports"), IGameDataExportsContext;
