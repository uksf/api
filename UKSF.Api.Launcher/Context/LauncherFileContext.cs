using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Launcher.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Launcher.Context {
    public interface ILauncherFileContext : IMongoContext<LauncherFile>, ICachedMongoContext { }

    public class LauncherFileContext : CachedMongoContext<LauncherFile>, ILauncherFileContext {
        public LauncherFileContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "launcherFiles") { }
    }
}
