using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IBuildsDataService : IDataService<ModpackBuild>, ICachedDataService {
        Task Update(ModpackBuild build, ModpackBuildStep buildStep);
        Task Update(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition);
    }
}
