﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Game;
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

        public ModpackRelease GetRelease(string version) => releaseService.GetRelease(version);

        public ModpackBuild GetBuild(string id) => buildsService.Data.GetSingle(x => x.id == id);

        public async Task NewBuild(string reference) {
            GithubCommit commit = await githubService.GetLatestReferenceCommit(reference);
            if (!string.IsNullOrEmpty(sessionService.GetContextId())) {
                commit.author = sessionService.GetContextEmail();
            }

            string version = await githubService.GetReferenceVersion(reference);
            ModpackBuild build = await buildsService.CreateDevBuild(version, commit);
            LogWrapper.AuditLog($"New build created ({GetBuildName(build)})");
            buildQueueService.QueueBuild(build);
        }

        public async Task Rebuild(ModpackBuild build) {
            LogWrapper.AuditLog($"Rebuild triggered for {GetBuildName(build)}.");
            ModpackBuild rebuild = await buildsService.CreateRebuild(build);
            buildQueueService.QueueBuild(rebuild);
        }

        public void CancelBuild(ModpackBuild build) {
            LogWrapper.AuditLog($"Build {GetBuildName(build)} cancelled");
            buildQueueService.Cancel(build.id);
        }

        public async Task UpdateReleaseDraft(ModpackRelease release) {
            LogWrapper.AuditLog($"Release {release.version} draft updated");
            await releaseService.UpdateDraft(release);
        }

        public async Task Release(string version) {
            ModpackBuild releaseBuild = await buildsService.CreateReleaseBuild(version);
            buildQueueService.QueueBuild(releaseBuild);

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
            string version = await githubService.GetReferenceVersion(payload.Ref);
            ModpackBuild devBuild = await buildsService.CreateDevBuild(version, devCommit);
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

        private static string GetBuildName(ModpackBuild build) =>
            build.environment switch {
                GameEnvironment.RELEASE => $"release {build.version}",
                GameEnvironment.RC => $"{build.version} RC# {build.buildNumber}",
                GameEnvironment.DEV => $"#{build.buildNumber}",
                _ => throw new ArgumentException("Invalid build environment")
            };
    }
}