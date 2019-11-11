using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Launcher;

namespace UKSFWebsite.Api.Data.Launcher {
    public class LauncherFileDataService : CachedDataService<LauncherFile>, ILauncherFileDataService {
        public LauncherFileDataService(IMongoDatabase database) : base(database, "launcherFiles") { }
    }
}
