using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack {
    public class ReleaseService : DataBackedService<IReleasesDataService>, IReleaseService {
        private readonly IAccountService accountService;
        private readonly IGithubService githubService;

        public ReleaseService(IReleasesDataService data, IGithubService githubService, IAccountService accountService) : base(data) {
            this.githubService = githubService;
            this.accountService = accountService;
        }

        public ModpackRelease GetRelease(string version) {
            return Data.GetSingle(x => x.version == version);
        }

        public async Task MakeDraftRelease(string version, GithubCommit commit) {
            string changelog = await githubService.GenerateChangelog(version);
            string creatorId = accountService.Data.GetSingle(x => x.email == commit.author)?.id;
            await Data.Add(new ModpackRelease { timestamp = DateTime.Now, version = version, changelog = changelog, isDraft = true, creatorId = creatorId });
        }

        public async Task UpdateDraft(ModpackRelease release) {
            await Data.Update(release.id, Builders<ModpackRelease>.Update.Set(x => x.description, release.description).Set(x => x.changelog, release.changelog));
        }

        public async Task PublishRelease(string version) {
            ModpackRelease release = GetRelease(version);
            if (release == null) {
                throw new NullReferenceException($"Could not find release {version}");
            }

            release.changelog += release.changelog.EndsWith("\n\n") ? "<br>" : "\n\n<br>";
            release.changelog += "SR3 - Development Team<br>[Report and track issues here](https://github.com/uksf/modpack/issues)";

            await Data.Update(release.id, Builders<ModpackRelease>.Update.Set(x => x.timestamp, DateTime.Now).Set(x => x.isDraft, false).Set(x => x.changelog, release.changelog));
            await githubService.PublishRelease(release);
        }

        public async Task AddHistoricReleases(IEnumerable<ModpackRelease> releases) {
            IEnumerable<ModpackRelease> existingReleases = Data.Get();
            foreach (ModpackRelease release in releases.Where(x => existingReleases.All(y => y.version != x.version))) {
                await Data.Add(release);
            }
        }
    }
}
