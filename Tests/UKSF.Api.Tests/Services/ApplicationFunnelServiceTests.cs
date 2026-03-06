using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class ApplicationFunnelServiceTests
{
    private readonly Mock<IApplicationFunnelEventContext> _mockContext = new();
    private readonly ApplicationFunnelService _sut;

    public ApplicationFunnelServiceTests()
    {
        _sut = new ApplicationFunnelService(_mockContext.Object);
    }

    [Fact]
    public void GetFunnel_Should_ReturnZeroCounts_When_NoEvents()
    {
        _mockContext.Setup(x => x.Get()).Returns([]);

        var result = _sut.GetFunnel();

        result.LastMonth.Stages.InfoPageViews.Should().Be(0);
        result.Total.Stages.InfoPageViews.Should().Be(0);
    }

    [Fact]
    public void GetFunnel_Should_CountEventsByType()
    {
        var events = new List<DomainApplicationFunnelEvent>
        {
            new()
            {
                Event = "info_page_view",
                VisitorId = "v1",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_view",
                VisitorId = "v2",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_view",
                VisitorId = "v3",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_next",
                VisitorId = "v1",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_next",
                VisitorId = "v2",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "account_created",
                VisitorId = "v1",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "application_submitted",
                VisitorId = "v1",
                Timestamp = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.Get()).Returns(events);

        var result = _sut.GetFunnel();

        result.Total.Stages.InfoPageViews.Should().Be(3);
        result.Total.Stages.InfoPageNext.Should().Be(2);
        result.Total.Stages.AccountCreated.Should().Be(1);
        result.Total.Stages.ApplicationSubmitted.Should().Be(1);

        result.LastMonth.Stages.InfoPageViews.Should().Be(3);
    }

    [Fact]
    public void GetFunnel_Should_CalculateAverageDurations()
    {
        var events = new List<DomainApplicationFunnelEvent>
        {
            new()
            {
                Event = "info_page_view",
                VisitorId = "v1",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_duration",
                VisitorId = "v1",
                Value = 120,
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_next",
                VisitorId = "v1",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_view",
                VisitorId = "v2",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_duration",
                VisitorId = "v2",
                Value = 10,
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_view",
                VisitorId = "v3",
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_duration",
                VisitorId = "v3",
                Value = 90,
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                Event = "info_page_next",
                VisitorId = "v3",
                Timestamp = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.Get()).Returns(events);

        var result = _sut.GetFunnel();

        result.Total.AvgDuration.Overall.Should().BeApproximately(73.33, 0.01);
        result.Total.AvgDuration.Bounced.Should().BeApproximately(10, 0.01);
        result.Total.AvgDuration.Proceeded.Should().BeApproximately(105, 0.01);
    }

    [Fact]
    public void GetFunnel_Should_SplitByTimePeriod()
    {
        var events = new List<DomainApplicationFunnelEvent>
        {
            new()
            {
                Event = "info_page_view",
                VisitorId = "old",
                Timestamp = DateTime.UtcNow.AddDays(-60)
            },
            new()
            {
                Event = "info_page_view",
                VisitorId = "recent",
                Timestamp = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.Get()).Returns(events);

        var result = _sut.GetFunnel();

        result.LastMonth.Stages.InfoPageViews.Should().Be(1);
        result.Total.Stages.InfoPageViews.Should().Be(2);
    }
}
