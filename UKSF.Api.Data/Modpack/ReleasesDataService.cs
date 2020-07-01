using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Data.Modpack {
        public class ReleasesDataService : CachedDataService<ModpackRelease, IReleasesDataService>, IReleasesDataService {
            public ReleasesDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IReleasesDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "modpackReleases") { }
        }
}
