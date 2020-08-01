using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Data.Modpack {
    public class ReleasesDataService : CachedDataService<ModpackRelease, IReleasesDataService>, IReleasesDataService {
        public ReleasesDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IReleasesDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "modpackReleases") { }

        public override List<ModpackRelease> Collection {
            get => base.Collection;
            protected set {
                lock (LockObject) {
                    base.Collection = value?.Select(
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
}
