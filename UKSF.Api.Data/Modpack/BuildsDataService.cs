using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Data.Modpack {
    public class BuildsDataService : CachedDataService<ModpackBuild>, IBuildsDataService {
        public BuildsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ModpackBuild> dataEventBus) : base(dataCollectionFactory, dataEventBus, "modpackBuilds") { }

        protected override void SetCache(IEnumerable<ModpackBuild> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderByDescending(x => x.buildNumber).ToList();
            }
        }

        public async Task Update(ModpackBuild build, ModpackBuildStep buildStep) {
            UpdateDefinition<ModpackBuild> updateDefinition = Builders<ModpackBuild>.Update.Set(x => x.steps[buildStep.index], buildStep);
            await base.Update(build.id, updateDefinition);
            DataEvent(EventModelFactory.CreateDataEvent<ModpackBuild>(DataEventType.UPDATE, build.id, buildStep));
        }

        public async Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition) {
            await base.Update(build.id, updateDefinition);
            DataEvent(EventModelFactory.CreateDataEvent<ModpackBuild>(DataEventType.UPDATE, build.id, build));
        }
    }
}
