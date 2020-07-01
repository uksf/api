using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Modpack;
using UKSF.Common;

namespace UKSF.Api.Data.Modpack {
    public class BuildsDataService : CachedDataService<ModpackBuildRelease, IBuildsDataService>, IBuildsDataService {
        public BuildsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IBuildsDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "modpackBuilds") { }

        public override List<ModpackBuildRelease> Get() {
            base.Get();
            Collection = Collection.OrderByDescending(x => x.version).ToList();
            Collection.ForEach(x => x.builds = x.builds.OrderByDescending(y => y.buildNumber).ToList());
            return Collection;
        }

        public async Task Update(string id, ModpackBuild build, DataEventType updateType) {
            UpdateDefinition<ModpackBuildRelease> updateDefinition;
            switch (updateType) {
                case DataEventType.ADD:
                    updateDefinition = Builders<ModpackBuildRelease>.Update.Push(x => x.builds, build);
                    await base.Update(id, updateDefinition);
                    break;
                case DataEventType.UPDATE:
                    updateDefinition = Builders<ModpackBuildRelease>.Update.Set(x => x.builds[-1], build);
                    await base.Update(x => x.id == id && x.builds.Any(y => y.buildNumber == build.buildNumber), updateDefinition);
                    break;
                case DataEventType.DELETE: return;
                default: throw new ArgumentOutOfRangeException(nameof(updateType), updateType, null);
            }

            Refresh();
            ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(updateType, GetSingle(id).version, build));
        }

        private void ModpackBuildDataEvent(DataEventModel<IBuildsDataService> dataEvent) {
            base.CachedDataEvent(dataEvent);
        }

        protected override void CachedDataEvent(DataEventModel<IBuildsDataService> dataEvent) {
            if (ObjectId.TryParse(dataEvent.id, out ObjectId unused)) {
                base.CachedDataEvent(dataEvent);
            }
        }
    }
}
