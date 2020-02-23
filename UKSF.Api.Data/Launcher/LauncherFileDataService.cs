using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Launcher;

namespace UKSF.Api.Data.Launcher {
    public class LauncherFileDataService : CachedDataService<LauncherFile, ILauncherFileDataService>, ILauncherFileDataService {
        public LauncherFileDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ILauncherFileDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "launcherFiles") { }
    }
}
