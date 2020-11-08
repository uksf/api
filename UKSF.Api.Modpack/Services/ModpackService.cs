using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services.BuildProcess;

namespace UKSF.Api.Modpack.Services {
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

    public class ModpackService : IModpackService {
        private readonly IBuildQueueService buildQueueService;
        private readonly IBuildsService buildsService;
        private readonly IGithubService githubService;
        private readonly IHttpContextService httpContextService;
        private readonly ILogger logger;
        private readonly IReleaseService releaseService;


        public ModpackService(IReleaseService releaseService, IBuildsService buildsService, IBuildQueueService buildQueueService, IGithubService githubService, IHttpContextService httpContextService, ILogger logger) {
            this.releaseService = releaseService;
            this.buildsService = buildsService;
            this.buildQueueService = buildQueueService;
            this.githubService = githubService;
            this.httpContextService = httpContextService;
            this.logger = logger;
        }

        public IEnumerable<ModpackRelease> GetReleases() => releaseService.Data.Get();

        public IEnumerable<ModpackBuild> GetRcBuilds() => buildsService.GetRcBuilds();

        public IEnumerable<ModpackBuild> GetDevBuilds() => buildsService.GetDevBuilds();

        public ModpackRelease GetRelease(string version) => releaseService.GetRelease(version);

        public ModpackBuild GetBuild(string id) => buildsService.Data.GetSingle(x => x.id == id);

        public async Task NewBuild(NewBuild newBuild) {
            GithubCommit commit = await githubService.GetLatestReferenceCommit(newBuild.reference);
            if (!string.IsNullOrEmpty(httpContextService.GetUserId())) {
                commit.author = httpContextService.GetUserEmail();
            }

            string version = await githubService.GetReferenceVersion(newBuild.reference);
            ModpackBuild build = await buildsService.CreateDevBuild(version, commit, newBuild);
            logger.LogAudit($"New build created ({GetBuildName(build)})");
            buildQueueService.QueueBuild(build);
        }

        public async Task Rebuild(ModpackBuild build) {
            logger.LogAudit($"Rebuild triggered for {GetBuildName(build)}.");
            ModpackBuild rebuild = await buildsService.CreateRebuild(build, build.commit.branch == "None" ? string.Empty : (await githubService.GetLatestReferenceCommit(build.commit.branch)).after);

            buildQueueService.QueueBuild(rebuild);
        }

        public async Task CancelBuild(ModpackBuild build) {
            logger.LogAudit($"Build {GetBuildName(build)} cancelled");

            if (buildQueueService.CancelQueued(build.id)) {
                await buildsService.CancelBuild(build);
            } else {
                buildQueueService.Cancel(build.id);
            }
        }

        public async Task UpdateReleaseDraft(ModpackRelease release) {
            logger.LogAudit($"Release {release.version} draft updated");
            await releaseService.UpdateDraft(release);
        }

        public async Task Release(string version) {
            ModpackBuild releaseBuild = await buildsService.CreateReleaseBuild(version);
            buildQueueService.QueueBuild(releaseBuild);

            logger.LogAudit($"{version} released");
        }

        public async Task RegnerateReleaseDraftChangelog(string version) {
            ModpackRelease release = releaseService.GetRelease(version);
            string newChangelog = await githubService.GenerateChangelog(version);
            release.changelog = newChangelog;

            logger.LogAudit($"Release {version} draft changelog regenerated from github");
            await releaseService.UpdateDraft(release);
        }

        public async Task CreateDevBuildFromPush(PushWebhookPayload payload) {
            GithubCommit devCommit = await githubService.GetPushEvent(payload);
            string version = await githubService.GetReferenceVersion(payload.Ref);
            ModpackBuild devBuild = await buildsService.CreateDevBuild(version, devCommit);
            buildQueueService.QueueBuild(devBuild);
        }

        public async Task CreateRcBuildFromPush(PushWebhookPayload payload) {
            string rcVersion = await githubService.GetReferenceVersion(payload.Ref);
            ModpackRelease release = releaseService.GetRelease(rcVersion);
            if (release != null && !release.isDraft) {
                logger.LogWarning($"An attempt to build a release candidate for version {rcVersion} failed because the version has already been released.");
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

        public void RunQueuedBuilds() {
            List<ModpackBuild> builds = buildsService.GetDevBuilds().Where(x => !x.finished && !x.running).ToList();
            builds = builds.Concat(buildsService.GetRcBuilds().Where(x => !x.finished && !x.running)).ToList();
            if (!builds.Any()) return;

            foreach (ModpackBuild build in builds) {
                buildQueueService.QueueBuild(build);
            }
        }

        private static string GetBuildName(ModpackBuild build) =>
            build.environment switch {
                GameEnvironment.RELEASE => $"release {build.version}",
                GameEnvironment.RC => $"{build.version} RC# {build.buildNumber}",
                GameEnvironment.DEV => $"#{build.buildNumber}",
                _ => throw new ArgumentException("Invalid build environment")
            };
    }
}
