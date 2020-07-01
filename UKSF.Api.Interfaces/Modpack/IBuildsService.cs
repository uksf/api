using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack {
    public interface IBuildsService : IDataBackedService<IBuildsDataService> {
        Task InsertBuild(string id, ModpackBuild build);
        Task UpdateBuild(string id, ModpackBuild build);
        Task CreateDevBuild(GithubPushEvent githubPushEvent);
        Task CreateRcBuild(GithubPushEvent githubPushEvent);
        Task<string> GetBranchVersion(string branch);
    }
}
