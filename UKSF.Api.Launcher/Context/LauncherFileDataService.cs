using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Launcher.Models;

namespace UKSF.Api.Launcher.Context {
    public interface ILauncherFileDataService : IDataService<LauncherFile>, ICachedDataService { }

    public class LauncherFileDataService : CachedDataService<LauncherFile>, ILauncherFileDataService {
        public LauncherFileDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<LauncherFile> dataEventBus) : base(dataCollectionFactory, dataEventBus, "launcherFiles") { }
    }
}
