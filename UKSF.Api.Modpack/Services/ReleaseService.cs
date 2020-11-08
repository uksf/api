using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services.Data;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Modpack.Services {
    public interface IReleaseService : IDataBackedService<IReleasesDataService> {
        Task MakeDraftRelease(string version, GithubCommit commit);
        Task UpdateDraft(ModpackRelease release);
        Task PublishRelease(string version);
        ModpackRelease GetRelease(string version);
        Task AddHistoricReleases(IEnumerable<ModpackRelease> releases);
    }

    public class ReleaseService : DataBackedService<IReleasesDataService>, IReleaseService {
        private readonly IAccountService _accountService;
        private readonly ILogger _logger;
        private readonly IGithubService _githubService;

        public ReleaseService(IReleasesDataService data, IGithubService githubService, IAccountService accountService, ILogger logger) : base(data) {
            _githubService = githubService;
            _accountService = accountService;
            _logger = logger;
        }

        public ModpackRelease GetRelease(string version) {
            return Data.GetSingle(x => x.Version == version);
        }

        public async Task MakeDraftRelease(string version, GithubCommit commit) {
            string changelog = await _githubService.GenerateChangelog(version);
            string creatorId = _accountService.Data.GetSingle(x => x.email == commit.Author)?.id;
            await Data.Add(new ModpackRelease { Timestamp = DateTime.Now, Version = version, Changelog = changelog, IsDraft = true, CreatorId = creatorId });
        }

        public async Task UpdateDraft(ModpackRelease release) {
            await Data.Update(release.id, Builders<ModpackRelease>.Update.Set(x => x.Description, release.Description).Set(x => x.Changelog, release.Changelog));
        }

        public async Task PublishRelease(string version) {
            ModpackRelease release = GetRelease(version);
            if (release == null) {
                throw new NullReferenceException($"Could not find release {version}");
            }

            if (!release.IsDraft) {
                _logger.LogWarning($"Attempted to release {version} again. Halting publish");
            }

            release.Changelog += release.Changelog.EndsWith("\n\n") ? "<br>" : "\n\n<br>";
            release.Changelog += "SR3 - Development Team<br>[Report and track issues here](https://github.com/uksf/modpack/issues)";

            await Data.Update(release.id, Builders<ModpackRelease>.Update.Set(x => x.Timestamp, DateTime.Now).Set(x => x.IsDraft, false).Set(x => x.Changelog, release.Changelog));
            await _githubService.PublishRelease(release);
        }

        public async Task AddHistoricReleases(IEnumerable<ModpackRelease> releases) {
            IEnumerable<ModpackRelease> existingReleases = Data.Get();
            foreach (ModpackRelease release in releases.Where(x => existingReleases.All(y => y.Version != x.Version))) {
                await Data.Add(release);
            }
        }
    }
}
