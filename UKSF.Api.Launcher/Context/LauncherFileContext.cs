using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Launcher.Models;

namespace UKSF.Api.Launcher.Context;

public interface ILauncherFileContext : IMongoContext<LauncherFile>, ICachedMongoContext { }

public class LauncherFileContext : CachedMongoContext<LauncherFile>, ILauncherFileContext
{
    public LauncherFileContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "launcherFiles") { }
}
