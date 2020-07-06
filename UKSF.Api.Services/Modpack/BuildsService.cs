using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack {
    public class BuildsService : DataBackedService<IBuildsDataService>, IBuildsService {
        private readonly IBuildStepService buildStepService;

        public BuildsService(IBuildsDataService data, IBuildStepService buildStepService) : base(data) => this.buildStepService = buildStepService;

        public async Task InsertBuild(string id, ModpackBuild build) {
            await Data.Update(id, build, DataEventType.ADD);
        }

        public async Task UpdateBuild(string id, ModpackBuild build) {
            await Data.Update(id, build, DataEventType.UPDATE);
        }

        public async Task UpdateBuildStep(string id, ModpackBuild build, ModpackBuildStep buildStep) {
            await Data.Update(id, build, buildStep);
        }

        public ModpackBuildRelease GetBuildRelease(string version) => Data.GetSingle(x => x.version == version);

        public ModpackBuild GetLatestBuild(string version) {
            ModpackBuildRelease buildRelease = GetBuildRelease(version);
            if (buildRelease == null || buildRelease.builds.Count == 0) return null;

            return buildRelease.builds[0];
        }

        public async Task<ModpackBuild> CreateDevBuild(string version, GithubCommit commit) {
            ModpackBuildRelease buildRelease = GetBuildRelease(version);
            if (buildRelease == null) {
                // New version build
                ModpackBuild newBuild = new ModpackBuild { buildNumber = 0, isNewVersion = true, commit = commit, steps = buildStepService.GetStepsForNewVersion() };
                newBuild.commit.message = "New version (no content changes)";
                buildRelease = new ModpackBuildRelease { version = version, builds = new List<ModpackBuild> { newBuild } };
                await Data.Add(buildRelease);
                return newBuild;
            }

            ModpackBuild previousBuild = buildRelease.builds.First();
            if (previousBuild.isReleaseCandidate || previousBuild.isRelease) {
                throw new InvalidOperationException("Cannot push dev build when RC exists");
            }

            ModpackBuild build = new ModpackBuild { buildNumber = previousBuild.buildNumber + 1, commit = commit, steps = buildStepService.GetStepsForBuild() };
            await InsertBuild(buildRelease.id, build);
            return build;
        }

        public async Task<ModpackBuild> CreateFirstRcBuild(string version, ModpackBuild build) {
            ModpackBuildRelease buildRelease = GetBuildRelease(version);
            if (buildRelease == null) {
                throw new NullReferenceException($"Cannot create first RC build for build release {version} as it does not exist");
            }

            ModpackBuild newBuild = new ModpackBuild { buildNumber = build.buildNumber + 1, isReleaseCandidate = true, commit = new GithubCommit(), steps = buildStepService.GetStepsForRc() };
            await InsertBuild(buildRelease.id, newBuild);

            return newBuild;
        }

        public async Task<ModpackBuild> CreateRcBuild(string version, GithubCommit commit) {
            ModpackBuildRelease buildRelease = GetBuildRelease(version);
            if (buildRelease == null) {
                throw new NullReferenceException($"CI tried to create RC build for build release {version} which does not exist");
            }

            // This can't be the first RC build
            ModpackBuild previousBuild = buildRelease.builds.FirstOrDefault(x => x.isReleaseCandidate);
            if (previousBuild == null) {
                throw new InvalidOperationException("First RC build should not be created by CI. Something went wrong");
            }

            ModpackBuild build = new ModpackBuild { buildNumber = previousBuild.buildNumber + 1, commit = commit, isReleaseCandidate = true, steps = buildStepService.GetStepsForRc() };
            await InsertBuild(buildRelease.id, build);
            return build;
        }

        public async Task<ModpackBuild> CreateReleaseBuild(string version) {
            ModpackBuildRelease buildRelease = GetBuildRelease(version);
            if (buildRelease == null) {
                throw new NullReferenceException($"Tried to release version but build release {version} does not exist");
            }

            // There must be at least one RC build to release
            ModpackBuild previousBuild = buildRelease.builds.FirstOrDefault(x => x.isReleaseCandidate);
            if (previousBuild == null) {
                throw new InvalidOperationException("Release build requires a RC build");
            }

            ModpackBuild build = new ModpackBuild { buildNumber = previousBuild.buildNumber + 1, isRelease = true, steps = buildStepService.GetStepsForRelease(), commit = previousBuild.commit };
            build.commit.message = "Release deployment (no content changes)";
            await InsertBuild(buildRelease.id, build);
            return build;
        }

        public async Task<ModpackBuild> CreateRebuild(string version, ModpackBuild build) {
            ModpackBuildRelease buildRelease = GetBuildRelease(version);
            if (buildRelease == null) {
                throw new NullReferenceException($"Tried to rebuild {build.buildNumber} but build release {version} does not exist");
            }

            ModpackBuild rebuild = new ModpackBuild {
                buildNumber = build.buildNumber + 1,
                isReleaseCandidate = build.isReleaseCandidate,
                steps = build.isReleaseCandidate ? buildStepService.GetStepsForRc() : buildStepService.GetStepsForBuild(),
                commit = build.commit
            };
            rebuild.commit.message = $"Rebuild of {build.buildNumber}";
            await InsertBuild(buildRelease.id, rebuild);
            return rebuild;
        }

        public async Task SetBuildRunning(string id, ModpackBuild build) {
            build.running = true;
            await UpdateBuild(id, build);
        }

        public async Task SucceedBuild(string id, ModpackBuild build) {
            build.running = false;
            build.finished = true;
            build.buildResult = ModpackBuildResult.SUCCESS;
            await UpdateBuild(id, build);
        }

        public async Task FailBuild(string id, ModpackBuild build) {
            build.running = false;
            build.finished = true;
            build.buildResult = ModpackBuildResult.FAILED;
            await UpdateBuild(id, build);
        }

        public async Task CancelBuild(string id, ModpackBuild build) {
            build.running = false;
            build.finished = true;
            build.buildResult = ModpackBuildResult.CANCELLED;
            await UpdateBuild(id, build);
        }

        public async Task ResetBuild(string version, ModpackBuild build) {
            ModpackBuildRelease buildRelease = GetBuildRelease(version);
            build.running = false;
            build.finished = false;
            build.buildResult = ModpackBuildResult.NONE;
            build.steps = build.isReleaseCandidate ? buildStepService.GetStepsForRc() : buildStepService.GetStepsForBuild();
            await UpdateBuild(buildRelease.id, build);
        }
    }
}
