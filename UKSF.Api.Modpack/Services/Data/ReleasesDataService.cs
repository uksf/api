using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services.Data {
    public interface IReleasesDataService : IDataService<ModpackRelease>, ICachedDataService  { }

    public class ReleasesDataService : CachedDataService<ModpackRelease>, IReleasesDataService {
        public ReleasesDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ModpackRelease> dataEventBus) : base(dataCollectionFactory, dataEventBus, "modpackReleases") { }

        protected override void SetCache(IEnumerable<ModpackRelease> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.Select(
                                         x => {
                                             int[] parts = x.version.Split('.').Select(int.Parse).ToArray();
                                             return new { release = x, major = parts[0], minor = parts[1], patch = parts[2] };
                                         }
                                     )
                                     .OrderByDescending(x => x.major)
                                     .ThenByDescending(x => x.minor)
                                     .ThenByDescending(x => x.patch)
                                     .Select(x => x.release)
                                     .ToList();
            }
        }
    }
}
