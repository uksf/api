using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services.Data {
    public interface IBuildsDataService : IDataService<ModpackBuild>, ICachedDataService {
        Task Update(ModpackBuild build, ModpackBuildStep buildStep);
        Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition);
    }

    public class BuildsDataService : CachedDataService<ModpackBuild>, IBuildsDataService {
        public BuildsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ModpackBuild> dataEventBus) : base(dataCollectionFactory, dataEventBus, "modpackBuilds") { }

        protected override void SetCache(IEnumerable<ModpackBuild> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderByDescending(x => x.BuildNumber).ToList();
            }
        }

        public async Task Update(ModpackBuild build, ModpackBuildStep buildStep) {
            UpdateDefinition<ModpackBuild> updateDefinition = Builders<ModpackBuild>.Update.Set(x => x.Steps[buildStep.Index], buildStep);
            await base.Update(build.id, updateDefinition);
            DataEvent(EventModelFactory.CreateDataEvent<ModpackBuild>(DataEventType.UPDATE, build.id, buildStep));
        }

        public async Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition) {
            await base.Update(build.id, updateDefinition);
            DataEvent(EventModelFactory.CreateDataEvent<ModpackBuild>(DataEventType.UPDATE, build.id, build));
        }
    }
}
