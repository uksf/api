using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Octokit;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class GithubServiceTests
{
    private const string WebhookSecret = "test-webhook-secret";
    private readonly Mock<IGithubClientService> _mockGithubClientService = new();
    private readonly Mock<IVersionService> _mockVersionService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly GithubService _subject;

    public GithubServiceTests()
    {
        var appSettings = Options.Create(
            new AppSettings
            {
                Secrets = new AppSettings.SecretsConfig { Github = new AppSettings.SecretsConfig.GithubConfig { WebhookSecret = WebhookSecret } }
            }
        );

        _mockGithubClientService.Setup(x => x.RepoOrg).Returns("uksf");
        _mockGithubClientService.Setup(x => x.RepoName).Returns("modpack");

        _subject = new GithubService(_mockLogger.Object, appSettings, _mockGithubClientService.Object, _mockVersionService.Object);
    }

    private static string ComputeHmacSha1(string secret, string body)
    {
        var data = Encoding.UTF8.GetBytes(body);
        var secretData = Encoding.UTF8.GetBytes(secret);
        using HMACSHA1 hmac = new(secretData);
        var hash = hmac.ComputeHash(data);
        return $"sha1={BitConverter.ToString(hash).ToLower().Replace("-", "")}";
    }

    private void SetupGitHubClient(Mock<IRepositoryContentsClient> mockContentsClient)
    {
        var mockGitHubClient = new Mock<IGitHubClient>();
        var mockRepositoriesClient = new Mock<IRepositoriesClient>();
        mockGitHubClient.Setup(x => x.Repository).Returns(mockRepositoriesClient.Object);
        mockRepositoriesClient.Setup(x => x.Content).Returns(mockContentsClient.Object);

        var gitHubClient = new GitHubClient(new ProductHeaderValue("test"));
        _mockGithubClientService.Setup(x => x.GetAuthenticatedClient()).ReturnsAsync(gitHubClient);
    }

    [Fact]
    public void VerifySignature_ShouldReturnTrue_WhenSignatureIsValid()
    {
        var body = "{\"action\":\"push\",\"ref\":\"refs/heads/main\"}";
        var validSignature = ComputeHmacSha1(WebhookSecret, body);

        var result = _subject.VerifySignature(validSignature, body);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_ShouldReturnFalse_WhenSignatureIsInvalid()
    {
        var body = "{\"action\":\"push\"}";
        var invalidSignature = "sha1=0000000000000000000000000000000000000000";

        var result = _subject.VerifySignature(invalidSignature, body);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_ShouldReturnFalse_WhenSignatureIsEmpty()
    {
        var body = "{\"action\":\"push\"}";

        var result = _subject.VerifySignature("", body);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_ShouldReturnTrue_WhenBodyIsEmpty()
    {
        var body = "";
        var validSignature = ComputeHmacSha1(WebhookSecret, body);

        var result = _subject.VerifySignature(validSignature, body);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_ShouldReturnFalse_WhenSignatureUsesWrongSecret()
    {
        var body = "{\"test\":\"data\"}";
        var wrongSecretSignature = ComputeHmacSha1("wrong-secret", body);

        var result = _subject.VerifySignature(wrongSecretSignature, body);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_ShouldReturnFalse_WhenBodyIsModified()
    {
        var originalBody = "{\"action\":\"push\"}";
        var validSignature = ComputeHmacSha1(WebhookSecret, originalBody);
        var modifiedBody = "{\"action\":\"pull\"}";

        var result = _subject.VerifySignature(validSignature, modifiedBody);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_ShouldReturnTrue_ForLargeBody()
    {
        var body = new string('a', 10000);
        var validSignature = ComputeHmacSha1(WebhookSecret, body);

        var result = _subject.VerifySignature(validSignature, body);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_ShouldReturnTrue_ForBodyWithUnicodeCharacters()
    {
        var body = "{\"message\":\"日本語テスト\"}";
        var validSignature = ComputeHmacSha1(WebhookSecret, body);

        var result = _subject.VerifySignature(validSignature, body);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_ShouldReturnFalse_WhenSignatureMissesSha1Prefix()
    {
        var body = "{\"test\":\"data\"}";
        var fullSignature = ComputeHmacSha1(WebhookSecret, body);
        var signatureWithoutPrefix = fullSignature["sha1=".Length..];

        var result = _subject.VerifySignature(signatureWithoutPrefix, body);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_ShouldReturnFalse_WhenSignatureHasWrongPrefix()
    {
        var body = "{\"test\":\"data\"}";
        var fullSignature = ComputeHmacSha1(WebhookSecret, body);
        var wrongPrefixSignature = "sha256=" + fullSignature["sha1=".Length..];

        var result = _subject.VerifySignature(wrongPrefixSignature, body);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_ShouldReturnFalse_WhenSignatureHexIsUppercase()
    {
        var body = "test body with some hex chars";
        var validSignature = ComputeHmacSha1(WebhookSecret, body);
        var hexPart = validSignature["sha1=".Length..];
        var upperHexSignature = "sha1=" + hexPart.ToUpper();

        var result = _subject.VerifySignature(upperHexSignature, body);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_ShouldReturnTrue_ForBodyWithNewlines()
    {
        var body = "line1\nline2\r\nline3";
        var validSignature = ComputeHmacSha1(WebhookSecret, body);

        var result = _subject.VerifySignature(validSignature, body);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_ShouldReturnTrue_ForBodyWithSpecialCharacters()
    {
        var body = "{\"key\":\"value with spaces & special <chars> \\\"escaped\\\"\"}";
        var validSignature = ComputeHmacSha1(WebhookSecret, body);

        var result = _subject.VerifySignature(validSignature, body);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_ShouldProduceDeterministicResults()
    {
        var body = "deterministic test";
        var validSignature = ComputeHmacSha1(WebhookSecret, body);

        var result1 = _subject.VerifySignature(validSignature, body);
        var result2 = _subject.VerifySignature(validSignature, body);

        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_ShouldReturnFalse_WhenSignatureIsNull()
    {
        var body = "test";
        var validSignature = ComputeHmacSha1(WebhookSecret, body);

        // string.Equals handles null gracefully
        var result = _subject.VerifySignature(null!, body);

        result.Should().BeFalse();
    }

    private static Issue CreateIssue(string title, string body, int number, params string[] labelNames)
    {
        var labels = labelNames.Select((name, i) => new Label(i, "", name, "", "", "", false)).ToList();
        return new Issue(
            "",
            $"https://github.com/uksf/modpack/issues/{number}",
            "",
            "",
            number,
            ItemState.Open,
            title,
            body,
            null,
            null,
            labels,
            null,
            null,
            null,
            0,
            null,
            null,
            DateTimeOffset.UtcNow,
            null,
            number,
            "",
            false,
            null,
            null,
            null,
            null
        );
    }

    [Fact]
    public void BuildChangelog_WithNoIssuesOrMods_ReturnsEmptyString()
    {
        var result = GithubService.BuildChangelog([], []);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildChangelog_WithOnlyIssues_ReturnsStandardChangelog()
    {
        var issues = new List<Issue> { CreateIssue("New feature", null, 1, "type/feature"), CreateIssue("Bug fix", null, 2, "type/bug fix") };

        var result = GithubService.BuildChangelog(issues, []);

        result.Should().Contain("#### Added");
        result.Should().Contain("- New feature [(#1)](https://github.com/uksf/modpack/issues/1)");
        result.Should().Contain("#### Fixed");
        result.Should().Contain("- Bug fix [(#2)](https://github.com/uksf/modpack/issues/2)");
    }

    [Fact]
    public void BuildChangelog_WithOnlyWorkshopMods_ReturnsModEntries()
    {
        var mods = new List<DomainWorkshopMod>
        {
            new()
            {
                Name = "CBA_A3",
                SteamId = "450814997",
                Status = WorkshopModStatus.InstalledPendingRelease
            },
            new()
            {
                Name = "ACE3",
                SteamId = "463939057",
                Status = WorkshopModStatus.UpdatedPendingRelease
            },
            new()
            {
                Name = "Old Mod",
                SteamId = "111111111",
                Status = WorkshopModStatus.UninstalledPendingRelease,
                ModpackVersionFirstAdded = "1.0.0"
            }
        };

        var result = GithubService.BuildChangelog([], mods);

        result.Should().Contain("#### Added\n- CBA_A3\n\n");
        result.Should().Contain("#### Updated\n- ACE3\n\n");
        result.Should().Contain("#### Removed\n- Old Mod\n\n");
    }

    [Fact]
    public void BuildChangelog_MergesModsIntoExistingSections()
    {
        var issues = new List<Issue> { CreateIssue("Zulu feature", null, 1, "type/feature") };
        var mods = new List<DomainWorkshopMod>
        {
            new()
            {
                Name = "Alpha Mod",
                SteamId = "12345",
                Status = WorkshopModStatus.InstalledPendingRelease
            }
        };

        var result = GithubService.BuildChangelog(issues, mods);

        result.Should().Contain("#### Added");
        var addedSection = result.Split("#### Added")[1].Split("\n\n")[0];
        addedSection.Should().Contain("- Alpha Mod");
        addedSection.Should().Contain("- Zulu feature");
        addedSection.IndexOf("Alpha Mod", StringComparison.Ordinal).Should().BeLessThan(addedSection.IndexOf("Zulu feature", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildChangelog_DeduplicatesModWhenIssueBodyContainsSteamId()
    {
        var issues = new List<Issue> { CreateIssue("Add CBA_A3", "https://steamcommunity.com/sharedfiles/filedetails/?id=463939057", 1, "type/mod addition") };
        var mods = new List<DomainWorkshopMod>
        {
            new()
            {
                Name = "CBA_A3",
                SteamId = "463939057",
                Status = WorkshopModStatus.InstalledPendingRelease
            }
        };

        var result = GithubService.BuildChangelog(issues, mods);

        result.Should().Contain("#### Added");
        var addedSection = result.Split("#### Added")[1].Split("\n\n")[0];
        addedSection.Split("CBA_A3").Should().HaveCount(2); // appears once (split gives 2 parts)
        addedSection.Should().Contain("[(#1)]"); // the issue entry, not the mod entry
    }

    [Fact]
    public void BuildChangelog_DoesNotDeduplicateWhenIssueBodyIsNull()
    {
        var issues = new List<Issue> { CreateIssue("Add some mod", null, 1, "type/mod addition") };
        var mods = new List<DomainWorkshopMod>
        {
            new()
            {
                Name = "Some Mod",
                SteamId = "463939057",
                Status = WorkshopModStatus.InstalledPendingRelease
            }
        };

        var result = GithubService.BuildChangelog(issues, mods);

        var addedSection = result.Split("#### Added")[1].Split("\n\n")[0];
        addedSection.Should().Contain("- Add some mod");
        addedSection.Should().Contain("- Some Mod");
    }

    [Fact]
    public void BuildChangelog_ExcludesUnreleasedModsFromRemoved()
    {
        var mods = new List<DomainWorkshopMod>
        {
            new()
            {
                Name = "Never Released Mod",
                SteamId = "12345",
                Status = WorkshopModStatus.UninstalledPendingRelease,
                ModpackVersionFirstAdded = null
            }
        };

        var result = GithubService.BuildChangelog([], mods);

        result.Should().NotContain("#### Removed");
        result.Should().NotContain("Never Released Mod");
    }

    [Fact]
    public void BuildChangelog_IncludesPreviouslyReleasedModInRemoved()
    {
        var mods = new List<DomainWorkshopMod>
        {
            new()
            {
                Name = "Old Mod",
                SteamId = "12345",
                Status = WorkshopModStatus.UninstalledPendingRelease,
                ModpackVersionFirstAdded = "1.0.0"
            }
        };

        var result = GithubService.BuildChangelog([], mods);

        result.Should().Contain("#### Removed");
        result.Should().Contain("- Old Mod");
    }

    [Fact]
    public void BuildChangelog_MergesUpdatedModsWithSpecialIssueFormat()
    {
        var issues = new List<Issue> { CreateIssue("CBA_A3 1.2.3", null, 1, "type/mod update") };
        var mods = new List<DomainWorkshopMod>
        {
            new()
            {
                Name = "ACE3",
                SteamId = "463939057",
                Status = WorkshopModStatus.UpdatedPendingRelease
            }
        };

        var result = GithubService.BuildChangelog(issues, mods);

        result.Should().Contain("#### Updated");
        var updatedSection = result.Split("#### Updated")[1].Split("\n\n")[0];
        updatedSection.Should().Contain("- ACE3");
        updatedSection.Should().Contain("- CBA_A3 to [1.2.3]");
        updatedSection.IndexOf("ACE3", StringComparison.Ordinal).Should().BeLessThan(updatedSection.IndexOf("CBA_A3", StringComparison.Ordinal));
    }
}
