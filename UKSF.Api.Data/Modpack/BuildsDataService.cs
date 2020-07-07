using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Modpack;
using UKSF.Common;

namespace UKSF.Api.Data.Modpack {
    public class BuildsDataService : CachedDataService<ModpackBuild, IBuildsDataService>, IBuildsDataService {
        public BuildsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IBuildsDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "modpackBuilds") { }

        public override List<ModpackBuild> Get() {
            base.Get();
            Collection = Collection.OrderByDescending(x => x.buildNumber).ToList();
            return Collection;
        }

        public async Task Update(ModpackBuild build, ModpackBuildStep buildStep) {
            UpdateDefinition<ModpackBuild> updateDefinition = Builders<ModpackBuild>.Update.Set(x => x.steps[buildStep.index], buildStep);
            await base.Update(x => x.id == build.id, updateDefinition);
            Refresh();
            CachedDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.UPDATE, build.id, buildStep));
        }

        public async Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition) {
            await base.Update(build.id, updateDefinition);
            CachedDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.UPDATE, build.id, build));
        }
    }
}
