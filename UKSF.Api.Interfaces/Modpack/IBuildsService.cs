using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack {
    public interface IBuildsService : IDataBackedService<IBuildsDataService> {
        ModpackBuildRelease GetBuildRelease(string version);
        ModpackBuild GetLatestBuild(string version);
        Task InsertBuild(string id, ModpackBuild build);
        Task UpdateBuild(string id, ModpackBuild build);
        Task UpdateBuildStep(string id, ModpackBuild build, ModpackBuildStep buildStep);
        Task<ModpackBuild> CreateDevBuild(string version, GithubCommit commit);
        Task<ModpackBuild> CreateFirstRcBuild(string version, ModpackBuild build);
        Task<ModpackBuild> CreateRcBuild(string version, GithubCommit commit);
        Task<ModpackBuild> CreateReleaseBuild(string version);
        Task SetBuildRunning(string id, ModpackBuild build);
        Task SucceedBuild(string id, ModpackBuild build);
        Task FailBuild(string id, ModpackBuild build);
        Task CancelBuild(string id, ModpackBuild build);
        Task ResetBuild(string version, ModpackBuild build);
        Task<ModpackBuild> CreateRebuild(string version, ModpackBuild build);
    }
}
