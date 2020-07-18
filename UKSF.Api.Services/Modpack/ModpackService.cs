using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
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

        public async Task NewBuild(string reference) {
            GithubCommit commit = await githubService.GetLatestReferenceCommit(reference);
            if (!string.IsNullOrEmpty(sessionService.GetContextId())) {
                commit.author = sessionService.GetContextEmail();
            }

            ModpackBuild build = await buildsService.CreateDevBuild(commit);
            LogWrapper.AuditLog($"New build created ({build.buildNumber})");
            buildQueueService.QueueBuild(build);
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

        public async Task RegnerateReleaseDraftChangelog(string version) {
            ModpackRelease release = releaseService.GetRelease(version);
            string newChangelog = await githubService.GenerateChangelog(version);
            release.changelog = newChangelog;

            LogWrapper.AuditLog($"Release {version} draft changelog regenerated from github");
            await releaseService.UpdateDraft(release);
        }

        public async Task CreateDevBuildFromPush(PushWebhookPayload payload) {
            GithubCommit devCommit = await githubService.GetPushEvent(payload);
            ModpackBuild devBuild = await buildsService.CreateDevBuild(devCommit);
            buildQueueService.QueueBuild(devBuild);
        }

        public async Task CreateRcBuildFromPush(PushWebhookPayload payload) {
            string rcVersion = await githubService.GetReferenceVersion(payload.Ref);
            ModpackRelease release = releaseService.GetRelease(rcVersion);
            if (release != null && !release.isDraft) {
                LogWrapper.Log($"An attempt to build a release candidate for version {rcVersion} failed because the version has already been released.");
                return;
            }

            ModpackBuild previousBuild = buildsService.GetLatestRcBuild(rcVersion);
            GithubCommit rcCommit = await githubService.GetPushEvent(payload, previousBuild != null ? previousBuild.commit.after : string.Empty);
            if (previousBuild == null) {
                await releaseService.MakeDraftRelease(rcVersion, rcCommit);
            }

            ModpackBuild rcBuild = await buildsService.CreateRcBuild(rcVersion, rcCommit);
            buildQueueService.QueueBuild(rcBuild);
        }
    }
}
