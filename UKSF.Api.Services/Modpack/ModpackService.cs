using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Services.Modpack {
    public class ModpackService : IModpackService {
        private readonly IBuildQueueService buildQueueService;
        private readonly IBuildsService buildsService;
        private readonly IGithubService githubService;
        private readonly IReleaseService releaseService;
        private readonly ISessionService sessionService;

        public ModpackService(IReleaseService releaseService, IBuildsService buildsService, IBuildQueueService buildQueueService, IGithubService githubService, ISessionService sessionService) {
            this.releaseService = releaseService;
            this.buildsService = buildsService;
            this.buildQueueService = buildQueueService;
            this.githubService = githubService;
            this.sessionService = sessionService;
        }

        public List<ModpackRelease> GetReleases() => releaseService.Data.Get();

        public List<ModpackBuild> GetRcBuilds() => buildsService.GetRcBuilds();

        public List<ModpackBuild> GetDevBuilds() => buildsService.GetDevBuilds();

        public ModpackBuild GetBuild(string id) => buildsService.Data.GetSingle(x => x.id == id);

        public async Task<bool> NewBuild(string reference) {
            if (!await githubService.IsReferenceValid(reference)) {
                return false;
            }

            GithubCommit commit = await githubService.GetLatestReferenceCommit(reference);
            if (!string.IsNullOrEmpty(sessionService.GetContextId())) {
                commit.author = sessionService.GetContextEmail();
            }

            ModpackBuild build = await buildsService.CreateDevBuild(commit);
            LogWrapper.AuditLog($"New build created ({build.buildNumber})");
            buildQueueService.QueueBuild(build);
            return true;
        }

        public async Task Rebuild(ModpackBuild build) {
            LogWrapper.AuditLog($"Rebuild triggered for {build.buildNumber}.");
            ModpackBuild rebuild = await buildsService.CreateRebuild(build);
            buildQueueService.QueueBuild(rebuild);
        }

        public void CancelBuild(ModpackBuild build) {
            LogWrapper.AuditLog($"Build {build.buildNumber} cancelled");
            buildQueueService.Cancel(build.id);
        }

        public async Task UpdateReleaseDraft(ModpackRelease release) {
            LogWrapper.AuditLog($"Release {release.version} draft updated");
            await releaseService.UpdateDraft(release);
        }

        public async Task Release(string version) {
            await releaseService.PublishRelease(version);
            ModpackBuild releaseBuild = await buildsService.CreateReleaseBuild(version);
            buildQueueService.QueueBuild(releaseBuild);
            await githubService.MergeBranch("dev", "release", $"Release {version}");
            await githubService.MergeBranch("master", "dev", $"Release {version}");

            LogWrapper.AuditLog($"{version} released");
        }
    }
}
