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
        private readonly IBuildQueueService _buildQueueService;
        private readonly IBuildsService _buildsService;
        private readonly IGithubService _githubService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;
        private readonly IReleaseService _releaseService;


        public ModpackService(IReleaseService releaseService, IBuildsService buildsService, IBuildQueueService buildQueueService, IGithubService githubService, IHttpContextService httpContextService, ILogger logger) {
            _releaseService = releaseService;
            _buildsService = buildsService;
            _buildQueueService = buildQueueService;
            _githubService = githubService;
            _httpContextService = httpContextService;
            _logger = logger;
        }

        public IEnumerable<ModpackRelease> GetReleases() => _releaseService.Data.Get();

        public IEnumerable<ModpackBuild> GetRcBuilds() => _buildsService.GetRcBuilds();

        public IEnumerable<ModpackBuild> GetDevBuilds() => _buildsService.GetDevBuilds();

        public ModpackRelease GetRelease(string version) => _releaseService.GetRelease(version);

        public ModpackBuild GetBuild(string id) => _buildsService.Data.GetSingle(x => x.id == id);

        public async Task NewBuild(NewBuild newBuild) {
            GithubCommit commit = await _githubService.GetLatestReferenceCommit(newBuild.Reference);
            if (!string.IsNullOrEmpty(_httpContextService.GetUserId())) {
                commit.Author = _httpContextService.GetUserEmail();
            }

            string version = await _githubService.GetReferenceVersion(newBuild.Reference);
            ModpackBuild build = await _buildsService.CreateDevBuild(version, commit, newBuild);
            _logger.LogAudit($"New build created ({GetBuildName(build)})");
            _buildQueueService.QueueBuild(build);
        }

        public async Task Rebuild(ModpackBuild build) {
            _logger.LogAudit($"Rebuild triggered for {GetBuildName(build)}.");
            ModpackBuild rebuild = await _buildsService.CreateRebuild(build, build.Commit.Branch == "None" ? string.Empty : (await _githubService.GetLatestReferenceCommit(build.Commit.Branch)).After);

            _buildQueueService.QueueBuild(rebuild);
        }

        public async Task CancelBuild(ModpackBuild build) {
            _logger.LogAudit($"Build {GetBuildName(build)} cancelled");

            if (_buildQueueService.CancelQueued(build.id)) {
                await _buildsService.CancelBuild(build);
            } else {
                _buildQueueService.Cancel(build.id);
            }
        }

        public async Task UpdateReleaseDraft(ModpackRelease release) {
            _logger.LogAudit($"Release {release.Version} draft updated");
            await _releaseService.UpdateDraft(release);
        }

        public async Task Release(string version) {
            ModpackBuild releaseBuild = await _buildsService.CreateReleaseBuild(version);
            _buildQueueService.QueueBuild(releaseBuild);

            _logger.LogAudit($"{version} released");
        }

        public async Task RegnerateReleaseDraftChangelog(string version) {
            ModpackRelease release = _releaseService.GetRelease(version);
            string newChangelog = await _githubService.GenerateChangelog(version);
            release.Changelog = newChangelog;

            _logger.LogAudit($"Release {version} draft changelog regenerated from github");
            await _releaseService.UpdateDraft(release);
        }

        public async Task CreateDevBuildFromPush(PushWebhookPayload payload) {
            GithubCommit devCommit = await _githubService.GetPushEvent(payload);
            string version = await _githubService.GetReferenceVersion(payload.Ref);
            ModpackBuild devBuild = await _buildsService.CreateDevBuild(version, devCommit);
            _buildQueueService.QueueBuild(devBuild);
        }

        public async Task CreateRcBuildFromPush(PushWebhookPayload payload) {
            string rcVersion = await _githubService.GetReferenceVersion(payload.Ref);
            ModpackRelease release = _releaseService.GetRelease(rcVersion);
            if (release != null && !release.IsDraft) {
                _logger.LogWarning($"An attempt to build a release candidate for version {rcVersion} failed because the version has already been released.");
                return;
            }

            ModpackBuild previousBuild = _buildsService.GetLatestRcBuild(rcVersion);
            GithubCommit rcCommit = await _githubService.GetPushEvent(payload, previousBuild != null ? previousBuild.Commit.After : string.Empty);
            if (previousBuild == null) {
                await _releaseService.MakeDraftRelease(rcVersion, rcCommit);
            }

            ModpackBuild rcBuild = await _buildsService.CreateRcBuild(rcVersion, rcCommit);
            _buildQueueService.QueueBuild(rcBuild);
        }

        public void RunQueuedBuilds() {
            List<ModpackBuild> builds = _buildsService.GetDevBuilds().Where(x => !x.Finished && !x.Running).ToList();
            builds = builds.Concat(_buildsService.GetRcBuilds().Where(x => !x.Finished && !x.Running)).ToList();
            if (!builds.Any()) return;

            foreach (ModpackBuild build in builds) {
                _buildQueueService.QueueBuild(build);
            }
        }

        private static string GetBuildName(ModpackBuild build) =>
            build.Environment switch {
                GameEnvironment.RELEASE => $"release {build.Version}",
                GameEnvironment.RC => $"{build.Version} RC# {build.BuildNumber}",
                GameEnvironment.DEV => $"#{build.BuildNumber}",
                _ => throw new ArgumentException("Invalid build environment")
            };
    }
}
