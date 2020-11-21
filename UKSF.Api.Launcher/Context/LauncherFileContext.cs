using UKSF.Api.Base.Context;
using UKSF.Api.Launcher.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Launcher.Context {
    public interface ILauncherFileContext : IMongoContext<LauncherFile>, ICachedMongoContext { }

    public class LauncherFileContext : CachedMongoContext<LauncherFile>, ILauncherFileContext {
        public LauncherFileContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<LauncherFile> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "launcherFiles") { }
    }
}
