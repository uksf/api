using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IBuildsDataService : IDataService<ModpackBuild, IBuildsDataService>, ICachedDataService {
        Task Update(ModpackBuild build, ModpackBuildStep buildStep);
        Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition);
        void LogEvent(ModpackBuild build, ModpackBuildStep buildStep);
    }
}
