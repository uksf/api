using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Base.Services;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Message;

namespace UKSF.Api.Services.Modpack {
    public class BuildsService : DataBackedService<IBuildsDataService>, IBuildsService {
        private readonly IAccountService accountService;
        private readonly IHttpContextService httpContextService;
        private readonly IBuildStepService buildStepService;


        public BuildsService(IBuildsDataService data, IBuildStepService buildStepService, IAccountService accountService, IHttpContextService httpContextService) : base(data) {
            this.buildStepService = buildStepService;
            this.accountService = accountService;
            this.httpContextService = httpContextService;
        }

        public async Task UpdateBuild(ModpackBuild build, UpdateDefinition<ModpackBuild> updateDefinition) {
            await Data.Update(build, updateDefinition);
        }

        public async Task UpdateBuildStep(ModpackBuild build, ModpackBuildStep buildStep) {
            await Data.Update(build, buildStep);
        }

        public IEnumerable<ModpackBuild> GetDevBuilds() => Data.Get(x => x.environment == GameEnvironment.DEV);

        public IEnumerable<ModpackBuild> GetRcBuilds() => Data.Get(x => x.environment != GameEnvironment.DEV);

        public ModpackBuild GetLatestDevBuild() => GetDevBuilds().FirstOrDefault();

        public ModpackBuild GetLatestRcBuild(string version) => GetRcBuilds().FirstOrDefault(x => x.version == version);

        public async Task<ModpackBuild> CreateDevBuild(string version, GithubCommit commit, NewBuild newBuild = null) {
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

            if (previousBuild != null) {
                SetEnvironmentVariables(build, previousBuild, newBuild);
            }

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

            if (previousBuild != null) {
                SetEnvironmentVariables(build, previousBuild);
            }

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
                builderId = httpContextService.GetUserId(),
                steps = buildStepService.GetSteps(GameEnvironment.RELEASE)
            };
            build.commit.message = "Release deployment (no content changes)";
            await Data.Add(build);
            return build;
        }

        public async Task<ModpackBuild> CreateRebuild(ModpackBuild build, string newSha = "") {
            ModpackBuild latestBuild = build.environment == GameEnvironment.DEV ? GetLatestDevBuild() : GetLatestRcBuild(build.version);
            ModpackBuild rebuild = new ModpackBuild {
                version = latestBuild.environment == GameEnvironment.DEV ? null : latestBuild.version,
                buildNumber = latestBuild.buildNumber + 1,
                isRebuild = true,
                environment = latestBuild.environment,
                steps = buildStepService.GetSteps(build.environment),
                commit = latestBuild.commit,
                builderId = httpContextService.GetUserId(),
                environmentVariables = latestBuild.environmentVariables
            };
            if (!string.IsNullOrEmpty(newSha)) {
                rebuild.commit.after = newSha;
            }

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

        public void CancelInterruptedBuilds() {
            List<ModpackBuild> builds = Data.Get(x => x.running || x.steps.Any(y => y.running)).ToList();
            if (!builds.Any()) return;

            IEnumerable<Task> tasks = builds.Select(
                async build => {
                    ModpackBuildStep runningStep = build.steps.FirstOrDefault(x => x.running);
                    if (runningStep != null) {
                        runningStep.running = false;
                        runningStep.finished = true;
                        runningStep.endTime = DateTime.Now;
                        runningStep.buildResult = ModpackBuildResult.CANCELLED;
                        runningStep.logs.Add(new ModpackBuildStepLogItem { text = "\nBuild was interrupted", colour = "goldenrod" });
                        await Data.Update(build, runningStep);
                    }

                    await FinishBuild(build, ModpackBuildResult.CANCELLED);
                }
            );
            _ = Task.WhenAll(tasks);
            logger.LogAudit($"Marked {builds.Count} interrupted builds as cancelled", "SERVER");
        }

        private async Task FinishBuild(ModpackBuild build, ModpackBuildResult result) {
            build.running = false;
            build.finished = true;
            build.buildResult = result;
            build.endTime = DateTime.Now;
            await Data.Update(build, Builders<ModpackBuild>.Update.Set(x => x.running, false).Set(x => x.finished, true).Set(x => x.buildResult, result).Set(x => x.endTime, DateTime.Now));
        }

        private static void SetEnvironmentVariables(ModpackBuild build, ModpackBuild previousBuild, NewBuild newBuild = null) {
            CheckEnvironmentVariable(build, previousBuild, "ace_updated", "Build ACE", newBuild?.ace ?? false);
            CheckEnvironmentVariable(build, previousBuild, "acre_updated", "Build ACRE", newBuild?.acre ?? false);
            CheckEnvironmentVariable(build, previousBuild, "f35_updated", "Build F-35", newBuild?.f35 ?? false);
        }

        private static void CheckEnvironmentVariable(ModpackBuild build, ModpackBuild previousBuild, string key, string stepName, bool force) {
            if (force) {
                build.environmentVariables[key] = true;
                return;
            }

            if (previousBuild.environmentVariables.ContainsKey(key)) {
                bool updated = (bool) previousBuild.environmentVariables[key];
                if (updated) {
                    ModpackBuildStep step = previousBuild.steps.FirstOrDefault(x => x.name == stepName);
                    if (step != null && (!step.finished || step.buildResult == ModpackBuildResult.FAILED || step.buildResult == ModpackBuildResult.CANCELLED)) {
                        build.environmentVariables[key] = true;
                    }
                }
            }
        }
    }
}
