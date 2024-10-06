using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface IReleaseService
{
    Task<DomainModpackRelease> MakeDraftRelease(string version, GithubCommit commit);
    Task UpdateDraft(DomainModpackRelease release);
    Task PublishRelease(string version);
    DomainModpackRelease GetRelease(string version);
    Task AddHistoricReleases(IEnumerable<DomainModpackRelease> releases);
}

public class ReleaseService(IReleasesContext releasesContext, IAccountContext accountContext, IGithubService githubService, IUksfLogger logger)
    : IReleaseService
{
    public DomainModpackRelease GetRelease(string version)
    {
        return releasesContext.GetSingle(x => x.Version == version);
    }

    public async Task<DomainModpackRelease> MakeDraftRelease(string version, GithubCommit commit)
    {
        var changelog = await githubService.GenerateChangelog(version);
        var creatorId = accountContext.GetSingle(x => x.Email == commit.Author)?.Id;
        await releasesContext.Add(
            new DomainModpackRelease
            {
                Timestamp = DateTime.UtcNow,
                Version = version,
                Changelog = changelog,
                IsDraft = true,
                CreatorId = creatorId
            }
        );
        return GetRelease(version);
    }

    public async Task UpdateDraft(DomainModpackRelease release)
    {
        await releasesContext.Update(
            release.Id,
            Builders<DomainModpackRelease>.Update.Set(x => x.Changelog, release.Changelog)
        );
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
            logger.LogWarning($"Attempted to release {version} again. Halting publish");
        }

        release.Changelog += release.Changelog.EndsWith("\n\n") ? "<br>" : "\n\n<br>";
        release.Changelog += "SR3 - Development Team<br>[Report and track issues here](https://github.com/uksf/modpack/issues)";

        await releasesContext.Update(
            release.Id,
            Builders<DomainModpackRelease>.Update.Set(x => x.Timestamp, DateTime.UtcNow).Set(x => x.IsDraft, false).Set(x => x.Changelog, release.Changelog)
        );
        await githubService.PublishRelease(release);
    }

    public async Task AddHistoricReleases(IEnumerable<DomainModpackRelease> releases)
    {
        var existingReleases = releasesContext.Get();
        foreach (var release in releases.Where(x => existingReleases.All(y => y.Version != x.Version)))
        {
            await releasesContext.Add(release);
        }
    }
}
