using FluentAssertions;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class AnalyticsRateLimiterTests
{
    private readonly AnalyticsRateLimiter _sut = new();

    [Fact]
    public void IsRateLimited_Should_ReturnFalse_When_FirstRequest()
    {
        _sut.IsRateLimited("visitor-1").Should().BeFalse();
    }

    [Fact]
    public void IsRateLimited_Should_ReturnFalse_When_UnderLimit()
    {
        for (var i = 0; i < 29; i++)
        {
            _sut.IsRateLimited("visitor-2");
        }

        _sut.IsRateLimited("visitor-2").Should().BeFalse();
    }

    [Fact]
    public void IsRateLimited_Should_ReturnTrue_When_OverLimit()
    {
        for (var i = 0; i < 30; i++)
        {
            _sut.IsRateLimited("visitor-3");
        }

        _sut.IsRateLimited("visitor-3").Should().BeTrue();
    }

    [Fact]
    public void IsRateLimited_Should_TrackVisitorsSeparately()
    {
        for (var i = 0; i < 30; i++)
        {
            _sut.IsRateLimited("visitor-4");
        }

        _sut.IsRateLimited("visitor-5").Should().BeFalse();
    }
}
