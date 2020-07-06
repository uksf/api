using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
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
            // Collection = Collection.Select(
            //                            x => {
            //                                int[] parts = x.version.Split('.').Select(int.Parse).ToArray();
            //                                return new { buildRelease = x, major = parts[0], minor = parts[1], patch = parts[2] };
            //                            }
            //                        )
            //                        .OrderByDescending(x => x.major)
            //                        .ThenByDescending(x => x.minor)
            //                        .ThenByDescending(x => x.patch)
            //                        .Select(x => x.buildRelease)
            //                        .ToList();
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

        public async Task Update(string id, ModpackBuild build, ModpackBuildStep buildStep) {
            UpdateDefinition<ModpackBuildRelease> updateDefinition = Builders<ModpackBuildRelease>.Update.Set(x => x.builds[-1].steps[buildStep.index], buildStep);
            await base.Update(x => x.id == id && x.builds.Any(y => y.buildNumber == build.buildNumber), updateDefinition);
            Refresh();
            ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.UPDATE, $"{GetSingle(id).version}.{build.buildNumber}", buildStep));
        }

        public override async Task Add(ModpackBuildRelease data) {
            await base.Add(data);
            ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.ADD, data.GetIdValue(), data));
        }

        public override async Task Update(string id, string fieldName, object value) {
            await base.Update(id, fieldName, value);
            ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.UPDATE, id));
        }

        public override async Task Update(string id, UpdateDefinition<ModpackBuildRelease> update) {
            await base.Update(id, update);
            ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.UPDATE, id));
        }

        public override async Task Update(Expression<Func<ModpackBuildRelease, bool>> filterExpression, UpdateDefinition<ModpackBuildRelease> update) {
            await base.Update(filterExpression, update);
            List<string> ids = Get(filterExpression.Compile()).Select(x => x.GetIdValue()).ToList();
            ids.ForEach(x => ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.UPDATE, x)));
        }

        public override async Task UpdateMany(Func<ModpackBuildRelease, bool> predicate, UpdateDefinition<ModpackBuildRelease> update) {
            List<ModpackBuildRelease> items = Get(predicate);
            await base.UpdateMany(predicate, update);
            items.ForEach(x => ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.UPDATE, x.GetIdValue())));
        }

        public override async Task Replace(ModpackBuildRelease item) {
            await base.Replace(item);
            ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.UPDATE, item.GetIdValue()));
        }

        public override async Task Delete(string id) {
            await base.Delete(id);
            ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.DELETE, id));
        }

        public override async Task DeleteMany(Func<ModpackBuildRelease, bool> predicate) {
            List<ModpackBuildRelease> items = Get(predicate);
            await base.DeleteMany(predicate);
            items.ForEach(x => ModpackBuildDataEvent(EventModelFactory.CreateDataEvent<IBuildsDataService>(DataEventType.DELETE, x.GetIdValue())));
        }

        private void ModpackBuildDataEvent(DataEventModel<IBuildsDataService> dataEvent) {
            base.CachedDataEvent(dataEvent);
        }

        protected override void CachedDataEvent(DataEventModel<IBuildsDataService> dataEvent) { }
    }
}
