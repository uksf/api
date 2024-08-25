using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using UKSF.Api.Launcher.Models;

namespace UKSF.Api.Launcher.Context;

public interface ILauncherFileContext : IMongoContext<LauncherFile>, ICachedMongoContext;

public class LauncherFileContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<LauncherFile>(mongoCollectionFactory, eventBus, variablesService, "launcherFiles"), ILauncherFileContext;
