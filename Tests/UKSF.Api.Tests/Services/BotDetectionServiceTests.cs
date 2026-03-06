using FluentAssertions;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class BotDetectionServiceTests
{
    private readonly BotDetectionService _sut = new();

    [Fact]
    public void IsBot_Should_ReturnTrue_When_UserAgentIsNull()
    {
        _sut.IsBot(null).Should().BeTrue();
    }

    [Fact]
    public void IsBot_Should_ReturnTrue_When_UserAgentIsEmpty()
    {
        _sut.IsBot("").Should().BeTrue();
    }

    [Theory]
    [InlineData("Googlebot/2.1")]
    [InlineData("Mozilla/5.0 (compatible; bingbot/2.0)")]
    [InlineData("Mozilla/5.0 (compatible; YandexBot/3.0)")]
    [InlineData("facebookexternalhit/1.1")]
    [InlineData("Discordbot/2.0")]
    [InlineData("GPTBot/1.0")]
    [InlineData("ClaudeBot/1.0")]
    [InlineData("Twitterbot/1.0")]
    public void IsBot_Should_ReturnTrue_When_UserAgentMatchesKnownBot(string userAgent)
    {
        _sut.IsBot(userAgent).Should().BeTrue();
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)")]
    public void IsBot_Should_ReturnFalse_When_UserAgentIsRealBrowser(string userAgent)
    {
        _sut.IsBot(userAgent).Should().BeFalse();
    }
}
