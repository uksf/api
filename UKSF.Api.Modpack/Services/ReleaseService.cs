﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services.Data;
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
        private readonly IAccountService accountService;
        private readonly ILogger logger;
        private readonly IGithubService githubService;

        public ReleaseService(IReleasesDataService data, IGithubService githubService, IAccountService accountService, ILogger logger) : base(data) {
            this.githubService = githubService;
            this.accountService = accountService;
            this.logger = logger;
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

            if (!release.isDraft) {
                logger.LogWarning($"Attempted to release {version} again. Halting publish");
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