using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Octokit;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Services;
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
}
