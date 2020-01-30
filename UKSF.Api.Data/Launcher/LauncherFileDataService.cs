using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Launcher;

namespace UKSF.Api.Data.Launcher {
    public class LauncherFileDataService : CachedDataService<LauncherFile, ILauncherFileDataService>, ILauncherFileDataService {
        public LauncherFileDataService(IMongoDatabase database, IDataEventBus<ILauncherFileDataService> dataEventBus) : base(database, dataEventBus, "launcherFiles") { }
    }
}
