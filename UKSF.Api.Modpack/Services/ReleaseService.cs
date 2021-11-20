using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Modpack.Services
{
    public interface IReleaseService
    {
        Task MakeDraftRelease(string version, GithubCommit commit);
        Task UpdateDraft(ModpackRelease release);
        Task PublishRelease(string version);
        ModpackRelease GetRelease(string version);
        Task AddHistoricReleases(IEnumerable<ModpackRelease> releases);
    }

    public class ReleaseService : IReleaseService
    {
        private readonly IAccountContext _accountContext;
        private readonly IGithubService _githubService;
        private readonly ILogger _logger;
        private readonly IReleasesContext _releasesContext;

        public ReleaseService(IReleasesContext releasesContext, IAccountContext accountContext, IGithubService githubService, ILogger logger)
        {
            _releasesContext = releasesContext;
            _accountContext = accountContext;
            _githubService = githubService;
            _logger = logger;
        }

        public ModpackRelease GetRelease(string version)
        {
            return _releasesContext.GetSingle(x => x.Version == version);
        }

        public async Task MakeDraftRelease(string version, GithubCommit commit)
        {
            var changelog = await _githubService.GenerateChangelog(version);
            var creatorId = _accountContext.GetSingle(x => x.Email == commit.Author)?.Id;
            await _releasesContext.Add(new() { Timestamp = DateTime.UtcNow, Version = version, Changelog = changelog, IsDraft = true, CreatorId = creatorId });
        }

        public async Task UpdateDraft(ModpackRelease release)
        {
            await _releasesContext.Update(release.Id, Builders<ModpackRelease>.Update.Set(x => x.Description, release.Description).Set(x => x.Changelog, release.Changelog));
        }

        public async Task PublishRelease(string version)
        {
            var release = GetRelease(version);
            if (release == null)
            {
                throw new NullReferenceException($"Could not find release {version}");
            }

            if (!release.IsDraft)
            {
                _logger.LogWarning($"Attempted to release {version} again. Halting publish");
            }

            release.Changelog += release.Changelog.EndsWith("\n\n") ? "<br>" : "\n\n<br>";
            release.Changelog += "SR3 - Development Team<br>[Report and track issues here](https://github.com/uksf/modpack/issues)";

            await _releasesContext.Update(release.Id, Builders<ModpackRelease>.Update.Set(x => x.Timestamp, DateTime.UtcNow).Set(x => x.IsDraft, false).Set(x => x.Changelog, release.Changelog));
            await _githubService.PublishRelease(release);
        }

        public async Task AddHistoricReleases(IEnumerable<ModpackRelease> releases)
        {
            var existingReleases = _releasesContext.Get();
            foreach (var release in releases.Where(x => existingReleases.All(y => y.Version != x.Version)))
            {
                await _releasesContext.Add(release);
            }
        }
    }
}
