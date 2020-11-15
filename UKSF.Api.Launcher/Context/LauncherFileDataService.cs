using UKSF.Api.Base.Context;
using UKSF.Api.Launcher.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Launcher.Context {
    public interface ILauncherFileDataService : IDataService<LauncherFile>, ICachedDataService { }

    public class LauncherFileDataService : CachedDataService<LauncherFile>, ILauncherFileDataService {
        public LauncherFileDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<LauncherFile> dataEventBus) : base(dataCollectionFactory, dataEventBus, "launcherFiles") { }
    }
}
