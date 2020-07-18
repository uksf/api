using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack {
    public interface IModpackService {
        List<ModpackRelease> GetReleases();
        List<ModpackBuild> GetRcBuilds();
        List<ModpackBuild> GetDevBuilds();
        ModpackBuild GetBuild(string id);
        Task NewBuild(string reference);
        Task Rebuild(ModpackBuild build);
        void CancelBuild(ModpackBuild build);
        Task UpdateReleaseDraft(ModpackRelease release);
        Task Release(string version);
        Task RegnerateReleaseDraftChangelog(string version);
        Task CreateDevBuildFromPush(PushWebhookPayload payload);
        Task CreateRcBuildFromPush(PushWebhookPayload payload);
    }
}
