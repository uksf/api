using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class ReleaseServiceTests
{
    private readonly Mock<IReleasesContext> _mockReleasesContext = new();
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IGithubService> _mockGithubService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly ReleaseService _subject;

    public ReleaseServiceTests()
    {
        _subject = new ReleaseService(_mockReleasesContext.Object, _mockAccountContext.Object, _mockGithubService.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetRelease_returns_matching_release()
    {
        var releases = new List<DomainModpackRelease> { new() { Version = "1.0.0", Changelog = "First" }, new() { Version = "2.0.0", Changelog = "Second" } };
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(pred => releases.SingleOrDefault(pred));

        var result = _subject.GetRelease("2.0.0");

        result.Should().NotBeNull();
        result.Version.Should().Be("2.0.0");
        result.Changelog.Should().Be("Second");
    }

    [Fact]
    public void GetRelease_returns_null_when_not_found()
    {
        var releases = new List<DomainModpackRelease> { new() { Version = "1.0.0" } };
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(pred => releases.SingleOrDefault(pred));

        var result = _subject.GetRelease("9.9.9");

        result.Should().BeNull();
    }

    [Fact]
    public async Task MakeDraftRelease_creates_draft_with_changelog_and_creator()
    {
        var commit = new GithubCommit { Author = "test@example.com" };
        var account = new DomainAccount { Email = "test@example.com" };
        _mockGithubService.Setup(x => x.GenerateChangelog("3.0.0")).ReturnsAsync("changelog content");
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns<Func<DomainAccount, bool>>(pred => new List<DomainAccount> { account }.SingleOrDefault(pred));
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(_ => new DomainModpackRelease { Version = "3.0.0" });

        await _subject.MakeDraftRelease("3.0.0", commit);

        _mockReleasesContext.Verify(
            x => x.Add(
                It.Is<DomainModpackRelease>(r => r.Version == "3.0.0" && r.Changelog == "changelog content" && r.IsDraft == true && r.CreatorId == account.Id)
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task MakeDraftRelease_sets_null_creator_when_author_not_found()
    {
        var commit = new GithubCommit { Author = "unknown@example.com" };
        _mockGithubService.Setup(x => x.GenerateChangelog("3.0.0")).ReturnsAsync("changelog");
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns<Func<DomainAccount, bool>>(pred => new List<DomainAccount>().SingleOrDefault(pred));
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(_ => new DomainModpackRelease { Version = "3.0.0" });

        await _subject.MakeDraftRelease("3.0.0", commit);

        _mockReleasesContext.Verify(x => x.Add(It.Is<DomainModpackRelease>(r => r.CreatorId == null)), Times.Once);
    }

    [Fact]
    public async Task MakeDraftRelease_returns_created_release()
    {
        var commit = new GithubCommit { Author = "test@example.com" };
        var expectedRelease = new DomainModpackRelease { Version = "3.0.0", Changelog = "log" };
        _mockGithubService.Setup(x => x.GenerateChangelog("3.0.0")).ReturnsAsync("log");
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns((DomainAccount)null);
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(_ => expectedRelease);

        var result = await _subject.MakeDraftRelease("3.0.0", commit);

        result.Should().BeSameAs(expectedRelease);
    }

    [Fact]
    public async Task PublishRelease_throws_when_release_not_found()
    {
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(pred => new List<DomainModpackRelease>().SingleOrDefault(pred));

        var act = () => _subject.PublishRelease("9.9.9");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*9.9.9*");
    }

    [Fact]
    public async Task PublishRelease_halts_when_already_published()
    {
        var release = new DomainModpackRelease { Version = "1.0.0", IsDraft = false };
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(pred => new List<DomainModpackRelease> { release }.SingleOrDefault(pred));

        await _subject.PublishRelease("1.0.0");

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("1.0.0"))), Times.Once);
        _mockReleasesContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainModpackRelease>>()), Times.Never);
    }

    [Fact]
    public async Task PublishRelease_appends_footer_and_publishes()
    {
        var release = new DomainModpackRelease
        {
            Version = "1.0.0",
            IsDraft = true,
            Changelog = "Some changes"
        };
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(pred => new List<DomainModpackRelease> { release }.SingleOrDefault(pred));

        await _subject.PublishRelease("1.0.0");

        release.Changelog.Should().Be("Some changes\n\n<br>SR3 - Development Team<br>[Report and track issues here](https://github.com/uksf/modpack/issues)");
    }

    [Fact]
    public async Task PublishRelease_uses_br_when_changelog_ends_with_double_newline()
    {
        var release = new DomainModpackRelease
        {
            Version = "1.0.0",
            IsDraft = true,
            Changelog = "Some changes\n\n"
        };
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(pred => new List<DomainModpackRelease> { release }.SingleOrDefault(pred));

        await _subject.PublishRelease("1.0.0");

        release.Changelog.Should().Be("Some changes\n\n<br>SR3 - Development Team<br>[Report and track issues here](https://github.com/uksf/modpack/issues)");
    }

    [Fact]
    public async Task PublishRelease_calls_github_publish()
    {
        var release = new DomainModpackRelease
        {
            Version = "1.0.0",
            IsDraft = true,
            Changelog = "log"
        };
        _mockReleasesContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainModpackRelease, bool>>()))
                            .Returns<Func<DomainModpackRelease, bool>>(pred => new List<DomainModpackRelease> { release }.SingleOrDefault(pred));

        await _subject.PublishRelease("1.0.0");

        _mockGithubService.Verify(x => x.PublishRelease(release), Times.Once);
    }

    [Fact]
    public async Task AddHistoricReleases_adds_only_new_versions()
    {
        var existing = new List<DomainModpackRelease> { new() { Version = "1.0.0" } };
        _mockReleasesContext.Setup(x => x.Get()).Returns(existing);
        var input = new List<DomainModpackRelease> { new() { Version = "1.0.0" }, new() { Version = "2.0.0" } };

        await _subject.AddHistoricReleases(input);

        _mockReleasesContext.Verify(x => x.Add(It.Is<DomainModpackRelease>(r => r.Version == "2.0.0")), Times.Once);
        _mockReleasesContext.Verify(x => x.Add(It.Is<DomainModpackRelease>(r => r.Version == "1.0.0")), Times.Never);
    }

    [Fact]
    public async Task AddHistoricReleases_adds_nothing_when_all_exist()
    {
        var existing = new List<DomainModpackRelease> { new() { Version = "1.0.0" }, new() { Version = "2.0.0" } };
        _mockReleasesContext.Setup(x => x.Get()).Returns(existing);
        var input = new List<DomainModpackRelease> { new() { Version = "1.0.0" }, new() { Version = "2.0.0" } };

        await _subject.AddHistoricReleases(input);

        _mockReleasesContext.Verify(x => x.Add(It.IsAny<DomainModpackRelease>()), Times.Never);
    }
}
