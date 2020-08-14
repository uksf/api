using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Interfaces.Modpack {
    public interface IModpackService {
        IEnumerable<ModpackRelease> GetReleases();
        IEnumerable<ModpackBuild> GetRcBuilds();
        IEnumerable<ModpackBuild> GetDevBuilds();
        ModpackRelease GetRelease(string version);
        ModpackBuild GetBuild(string id);
        Task NewBuild(NewBuild newBuild);
        Task Rebuild(ModpackBuild build);
        Task CancelBuild(ModpackBuild build);
        Task UpdateReleaseDraft(ModpackRelease release);
        Task Release(string version);
        Task RegnerateReleaseDraftChangelog(string version);
        Task CreateDevBuildFromPush(PushWebhookPayload payload);
        Task CreateRcBuildFromPush(PushWebhookPayload payload);
        void RunQueuedBuilds();
    }
}
