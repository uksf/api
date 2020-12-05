using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Modpack.Context {
    public interface IBuildsContext : IMongoContext<ModpackBuild>, ICachedMongoContext {
        Task Update(ModpackBuild build, ModpackBuildStep buildStep);
        Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition);
    }

    public class BuildsContext : CachedMongoContext<ModpackBuild>, IBuildsContext {
        public BuildsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "modpackBuilds") { }

        public async Task Update(ModpackBuild build, ModpackBuildStep buildStep) {
            UpdateDefinition<ModpackBuild> updateDefinition = Builders<ModpackBuild>.Update.Set(x => x.Steps[buildStep.Index], buildStep);
            await base.Update(build.Id, updateDefinition);
            DataEvent(new EventModel(EventType.UPDATE, new ModpackBuildStepEventData(build.Id, buildStep)));
        }

        public async Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition) {
            await base.Update(build.Id, updateDefinition);
            DataEvent(new EventModel(EventType.UPDATE, build));
        }

        protected override void SetCache(IEnumerable<ModpackBuild> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderByDescending(x => x.BuildNumber).ToList();
            }
        }
    }
}
