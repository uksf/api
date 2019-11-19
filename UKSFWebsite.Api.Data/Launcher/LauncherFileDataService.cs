using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Launcher;

namespace UKSFWebsite.Api.Data.Launcher {
    public class LauncherFileDataService : CachedDataService<LauncherFile, ILauncherFileDataService>, ILauncherFileDataService {
        public LauncherFileDataService(IMongoDatabase database, IDataEventBus<ILauncherFileDataService> dataEventBus) : base(database, dataEventBus, "launcherFiles") { }
    }
}
