using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IGameConfigExportsContext : IMongoContext<DomainGameConfigExport>;

public class GameConfigExportsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainGameConfigExport>(mongoCollectionFactory, eventBus, "gameConfigExports"), IGameConfigExportsContext;
