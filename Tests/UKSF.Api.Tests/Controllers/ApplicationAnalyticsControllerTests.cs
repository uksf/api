using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class ApplicationAnalyticsControllerTests
{
    private readonly Mock<IApplicationFunnelEventContext> _mockContext = new();
    private readonly Mock<IBotDetectionService> _mockBotDetection = new();
    private readonly Mock<IAnalyticsRateLimiter> _mockRateLimiter = new();
    private readonly Mock<IApplicationFunnelService> _mockFunnelService = new();
    private readonly ApplicationAnalyticsController _sut;

    public ApplicationAnalyticsControllerTests()
    {
        _sut = new ApplicationAnalyticsController(_mockContext.Object, _mockBotDetection.Object, _mockRateLimiter.Object, _mockFunnelService.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        _sut.ControllerContext.HttpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 Real Browser";
    }

    [Fact]
    public async Task TrackEvent_Should_ReturnOk_When_ValidRequest()
    {
        _mockBotDetection.Setup(x => x.IsBot(It.IsAny<string>())).Returns(false);
        _mockRateLimiter.Setup(x => x.IsRateLimited(It.IsAny<string>())).Returns(false);

        var result = await _sut.TrackEvent(new TrackFunnelEventRequest { VisitorId = "test-visitor", Event = "info_page_view" });

        result.Should().BeOfType<OkResult>();
        _mockContext.Verify(x => x.Add(It.Is<DomainApplicationFunnelEvent>(e => e.VisitorId == "test-visitor" && e.Event == "info_page_view")), Times.Once);
    }

    [Fact]
    public async Task TrackEvent_Should_ReturnOk_When_Bot_ButNotStore()
    {
        _mockBotDetection.Setup(x => x.IsBot(It.IsAny<string>())).Returns(true);

        var result = await _sut.TrackEvent(new TrackFunnelEventRequest { VisitorId = "bot-visitor", Event = "info_page_view" });

        result.Should().BeOfType<OkResult>();
        _mockContext.Verify(x => x.Add(It.IsAny<DomainApplicationFunnelEvent>()), Times.Never);
    }

    [Fact]
    public async Task TrackEvent_Should_ReturnOk_When_RateLimited_ButNotStore()
    {
        _mockBotDetection.Setup(x => x.IsBot(It.IsAny<string>())).Returns(false);
        _mockRateLimiter.Setup(x => x.IsRateLimited(It.IsAny<string>())).Returns(true);

        var result = await _sut.TrackEvent(new TrackFunnelEventRequest { VisitorId = "spammy-visitor", Event = "info_page_view" });

        result.Should().BeOfType<OkResult>();
        _mockContext.Verify(x => x.Add(It.IsAny<DomainApplicationFunnelEvent>()), Times.Never);
    }

    [Fact]
    public async Task TrackEvent_Should_StoreValue_When_DurationEvent()
    {
        _mockBotDetection.Setup(x => x.IsBot(It.IsAny<string>())).Returns(false);
        _mockRateLimiter.Setup(x => x.IsRateLimited(It.IsAny<string>())).Returns(false);

        var result = await _sut.TrackEvent(
            new TrackFunnelEventRequest
            {
                VisitorId = "test-visitor",
                Event = "info_page_duration",
                Value = 45.5
            }
        );

        result.Should().BeOfType<OkResult>();
        _mockContext.Verify(x => x.Add(It.Is<DomainApplicationFunnelEvent>(e => e.Value == 45.5)), Times.Once);
    }

    [Fact]
    public void GetFunnel_Should_ReturnFunnelData()
    {
        var expected = new ApplicationFunnelResponse
        {
            LastMonth = new FunnelData { Stages = new FunnelStages { InfoPageViews = 10 }, AvgDuration = new FunnelDurations() },
            Total = new FunnelData { Stages = new FunnelStages { InfoPageViews = 20 }, AvgDuration = new FunnelDurations() }
        };
        _mockFunnelService.Setup(x => x.GetFunnel()).Returns(expected);

        var result = _sut.GetFunnel();

        result.Should().BeEquivalentTo(expected);
    }
}
