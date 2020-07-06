using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack {
    public class ReleaseService : DataBackedService<IReleasesDataService>, IReleaseService {
        private readonly IGithubService githubService;
        private readonly ISessionService sessionService;

        public ReleaseService(IReleasesDataService data, IGithubService githubService, ISessionService sessionService) : base(data) {
            this.githubService = githubService;
            this.sessionService = sessionService;
        }

        public ModpackRelease GetRelease(string version) {
            return Data.GetSingle(x => x.version == version);
        }

        public async Task MakeDraftRelease(string version, ModpackBuild build) {
            string changelog = await githubService.GenerateChangelog(version);
            await Data.Add(new ModpackRelease { timestamp = DateTime.Now, version = version, changelog = changelog, isDraft = true, creatorId = sessionService.GetContextId() });
        }

        public async Task UpdateDraft(ModpackRelease release) {
            await Data.Update(release.id, Builders<ModpackRelease>.Update.Set(x => x.description, release.description).Set(x => x.changelog, release.changelog));
        }

        public async Task PublishRelease(string version) {
            ModpackRelease release = GetRelease(version);
            if (release == null) {
                throw new NullReferenceException($"Could not find release {version}");
            }

            await Data.Update(release.id, Builders<ModpackRelease>.Update.Set(x => x.timestamp, DateTime.Now).Set(x => x.isDraft, false).Set(x => x.releaserId, sessionService.GetContextId()));
            await githubService.PublishRelease(release);
        }

        public async Task AddHistoricReleases(List<ModpackRelease> releases) {
            List<ModpackRelease> existingReleases = Data.Get();
            foreach (ModpackRelease release in releases.Where(x => existingReleases.All(y => y.version != x.version))) {
                await Data.Add(release);
            }
        }
    }
}
