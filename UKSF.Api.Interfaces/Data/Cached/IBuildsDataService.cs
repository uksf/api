using System.Threading.Tasks;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IBuildsDataService : IDataService<ModpackBuildRelease, IBuildsDataService>, ICachedDataService {
        Task Update(string id, ModpackBuild build, DataEventType updateType);
        Task Update(string id, ModpackBuild build, ModpackBuildStep buildStep);
    }
}
