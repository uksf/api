using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack {
    public class BuildsService : DataBackedService<IBuildsDataService>, IBuildsService {
        private readonly IGithubService githubService;

        public BuildsService(IBuildsDataService data, IGithubService githubService) : base(data) => this.githubService = githubService;

        public async Task InsertBuild(string id, ModpackBuild build) {
            await Data.Update(id, build, DataEventType.ADD);
        }

        public async Task UpdateBuild(string id, ModpackBuild build) {
            await Data.Update(id, build, DataEventType.UPDATE);
        }

        public async Task UpdateBuildStep(string id, ModpackBuild build) {
            await Data.Update(id, build, DataEventType.UPDATE);
        }

        public async Task CreateDevBuild(GithubPushEvent githubPushEvent) {
            string version = await GetBranchVersion(githubPushEvent.branch);
            ModpackBuildRelease buildRelease = Data.GetSingle(x => x.version == version);
            if (buildRelease == null) {
                buildRelease = new ModpackBuildRelease {
                    version = version, builds = new List<ModpackBuild> { new ModpackBuild { buildNumber = 0, isNewVersion = true, pushEvent = githubPushEvent } }
                };
                await Data.Add(buildRelease);
                return;
            }

            ModpackBuild previousBuild = buildRelease.builds.First();
            ModpackBuild build = new ModpackBuild { buildNumber = previousBuild.buildNumber + 1, pushEvent = githubPushEvent };

            await InsertBuild(buildRelease.id, build);
            // run build
        }

        public async Task CreateRcBuild(GithubPushEvent githubPushEvent) {
            string version = await GetBranchVersion(githubPushEvent.branch);
            ModpackBuildRelease buildRelease = Data.GetSingle(x => x.version == version);
            if (buildRelease == null) {
                throw new NullReferenceException($"CI tried to create RC build for build release {version} which does not exist");
            }

            //this can't be the first RC build
            ModpackBuild previousBuild = buildRelease.builds.FirstOrDefault();
            if (previousBuild == null) {
                throw new InvalidOperationException("First RC build should not be created by CI. Something went wrong");
            }

            ModpackBuild build = new ModpackBuild { buildNumber = previousBuild.buildNumber + 1, pushEvent = githubPushEvent, isReleaseCandidate = true };
            await InsertBuild(buildRelease.id, build);
            // run build
        }

        public async Task<string> GetBranchVersion(string branch) {
            branch = branch.Split('/')[^1];
            return await githubService.GetCommitVersion(branch);
        }
    }
}
