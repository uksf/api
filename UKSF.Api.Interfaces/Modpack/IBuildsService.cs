using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack {
    public interface IBuildsService : IDataBackedService<IBuildsDataService> {
        List<ModpackBuild> GetDevBuilds();
        List<ModpackBuild> GetRcBuilds();
        ModpackBuild GetLatestDevBuild();
        ModpackBuild GetLatestRcBuild(string version);
        Task UpdateBuildStep(ModpackBuild build, ModpackBuildStep buildStep);
        void BuildStepLogEvent(ModpackBuild build, ModpackBuildStep buildStep);
        Task<ModpackBuild> CreateDevBuild(string version, GithubCommit commit);
        Task<ModpackBuild> CreateRcBuild(string version, GithubCommit commit);
        Task<ModpackBuild> CreateReleaseBuild(string version);
        Task SetBuildRunning(ModpackBuild build);
        Task SucceedBuild(ModpackBuild build);
        Task FailBuild(ModpackBuild build);
        Task CancelBuild(ModpackBuild build);
        Task<ModpackBuild> CreateRebuild(ModpackBuild build);
    }
}
