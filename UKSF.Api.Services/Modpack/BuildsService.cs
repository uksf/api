﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack {
    public class BuildsService : DataBackedService<IBuildsDataService>, IBuildsService {
        private readonly IAccountService accountService;
        private readonly IBuildStepService buildStepService;
        private readonly ISessionService sessionService;

        public BuildsService(IBuildsDataService data, IBuildStepService buildStepService, IAccountService accountService, ISessionService sessionService) : base(data) {
            this.buildStepService = buildStepService;
            this.accountService = accountService;
            this.sessionService = sessionService;
        }

        public async Task UpdateBuild(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition) {
            await Data.Update(build, updateDefinition);
        }

        public async Task UpdateBuildStep(ModpackBuild build, ModpackBuildStep buildStep) {
            await Data.Update(build, buildStep);
        }

        public void BuildStepLogEvent(ModpackBuild build, ModpackBuildStep buildStep) {
            Data.LogEvent(build, buildStep);
        }

        public List<ModpackBuild> GetDevBuilds() => Data.Get(x => x.environment == GameEnvironment.DEV);

        public List<ModpackBuild> GetRcBuilds() => Data.Get(x => x.environment != GameEnvironment.DEV);

        public ModpackBuild GetLatestDevBuild() => GetDevBuilds().FirstOrDefault();

        public ModpackBuild GetLatestRcBuild(string version) => GetRcBuilds().FirstOrDefault(x => x.version == version);

        public async Task<ModpackBuild> CreateDevBuild(string version, GithubCommit commit) {
            ModpackBuild previousBuild = GetLatestDevBuild();
            string builderId = accountService.Data.GetSingle(x => x.email == commit.author)?.id;
            ModpackBuild build = new ModpackBuild {
                version = version,
                buildNumber = previousBuild?.buildNumber + 1 ?? 1,
                environment = GameEnvironment.DEV,
                commit = commit,
                builderId = builderId,
                steps = buildStepService.GetSteps(GameEnvironment.DEV)
            };
            await Data.Add(build);
            return build;
        }

        public async Task<ModpackBuild> CreateRcBuild(string version, GithubCommit commit) {
            ModpackBuild previousBuild = GetLatestRcBuild(version);
            string builderId = accountService.Data.GetSingle(x => x.email == commit.author)?.id;
            ModpackBuild build = new ModpackBuild {
                version = version,
                buildNumber = previousBuild?.buildNumber + 1 ?? 1,
                environment = GameEnvironment.RC,
                commit = commit,
                builderId = builderId,
                steps = buildStepService.GetSteps(GameEnvironment.RC)
            };

            await Data.Add(build);
            return build;
        }

        public async Task<ModpackBuild> CreateReleaseBuild(string version) {
            // There must be at least one RC build to release
            ModpackBuild previousBuild = GetRcBuilds().FirstOrDefault(x => x.version == version);
            if (previousBuild == null) {
                throw new InvalidOperationException("Release build requires at leaste one RC build");
            }

            ModpackBuild build = new ModpackBuild {
                version = version,
                buildNumber = previousBuild.buildNumber + 1,
                environment = GameEnvironment.RELEASE,
                commit = previousBuild.commit,
                builderId = sessionService.GetContextId(),
                steps = buildStepService.GetSteps(GameEnvironment.RELEASE)
            };
            build.commit.message = "Release deployment (no content changes)";
            await Data.Add(build);
            return build;
        }

        public async Task<ModpackBuild> CreateRebuild(ModpackBuild build) {
            ModpackBuild latestBuild = build.environment == GameEnvironment.DEV ? GetLatestDevBuild() : GetLatestRcBuild(build.version);
            ModpackBuild rebuild = new ModpackBuild {
                version = latestBuild.environment == GameEnvironment.DEV ? null : latestBuild.version,
                buildNumber = latestBuild.buildNumber + 1,
                isRebuild = true,
                environment = latestBuild.environment,
                steps = buildStepService.GetSteps(build.environment),
                commit = latestBuild.commit,
                builderId = sessionService.GetContextId()
            };
            rebuild.commit.message = latestBuild.environment == GameEnvironment.RELEASE
                ? $"Re-deployment of release {rebuild.version}"
                : $"Rebuild of #{build.buildNumber}\n\n{rebuild.commit.message}";
            await Data.Add(rebuild);
            return rebuild;
        }

        public async Task SetBuildRunning(ModpackBuild build) {
            build.running = true;
            build.startTime = DateTime.Now;
            await Data.Update(build, Builders<ModpackBuild>.Update.Set(x => x.running, true).Set(x => x.startTime, DateTime.Now));
        }

        public async Task SucceedBuild(ModpackBuild build) {
            await FinishBuild(build, build.steps.Any(x => x.buildResult == ModpackBuildResult.WARNING) ? ModpackBuildResult.WARNING : ModpackBuildResult.SUCCESS);
        }

        public async Task FailBuild(ModpackBuild build) {
            await FinishBuild(build, ModpackBuildResult.FAILED);
        }

        public async Task CancelBuild(ModpackBuild build) {
            await FinishBuild(build, build.steps.Any(x => x.buildResult == ModpackBuildResult.WARNING) ? ModpackBuildResult.WARNING : ModpackBuildResult.CANCELLED);
        }

        private async Task FinishBuild(ModpackBuild build, ModpackBuildResult result) {
            build.running = false;
            build.finished = true;
            build.buildResult = result;
            build.endTime = DateTime.Now;
            await Data.Update(build, Builders<ModpackBuild>.Update.Set(x => x.running, false).Set(x => x.finished, true).Set(x => x.buildResult, result).Set(x => x.endTime, DateTime.Now));
        }
    }
}