using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using Xunit;
using SortDirection = UKSF.Api.Core.Models.SortDirection;

namespace UKSF.Api.Tests.Controllers;

public class LoggingControllerTests
{
    private readonly Mock<ILogContext> _mockLogContext;
    private readonly Mock<IAuditLogContext> _mockAuditLogContext;
    private readonly Mock<IErrorLogContext> _mockErrorLogContext;
    private readonly Mock<ILauncherLogContext> _mockLauncherLogContext;
    private readonly Mock<IDiscordLogContext> _mockDiscordLogContext;
    private readonly LoggingController _controller;

    public LoggingControllerTests()
    {
        _mockLogContext = new Mock<ILogContext>();
        _mockAuditLogContext = new Mock<IAuditLogContext>();
        _mockErrorLogContext = new Mock<IErrorLogContext>();
        _mockLauncherLogContext = new Mock<ILauncherLogContext>();
        _mockDiscordLogContext = new Mock<IDiscordLogContext>();

        _controller = new LoggingController(
            _mockLogContext.Object,
            _mockAuditLogContext.Object,
            _mockErrorLogContext.Object,
            _mockLauncherLogContext.Object,
            _mockDiscordLogContext.Object
        );
    }

    [Fact]
    public void GetBasicLogs_WithLevels_CallsGetPagedWithNonNullAdditionalFilter()
    {
        var expectedResult = new PagedResult<DomainBasicLog>(0, []);
        _mockLogContext.Setup(x => x.GetPaged(
                                  It.IsAny<int>(),
                                  It.IsAny<int>(),
                                  It.IsAny<SortDirection>(),
                                  It.IsAny<string>(),
                                  It.IsAny<IEnumerable<Expression<Func<DomainBasicLog, object>>>>(),
                                  It.IsAny<string>(),
                                  It.IsAny<FilterDefinition<DomainBasicLog>>()
                              )
                       )
                       .Returns(expectedResult);

        var result = _controller.GetBasicLogs(1, 10, SortDirection.Descending, "Timestamp", "", "Info,Error");

        result.Should().BeSameAs(expectedResult);
        _mockLogContext.Verify(
            x => x.GetPaged(
                1,
                10,
                SortDirection.Descending,
                "Timestamp",
                It.IsAny<IEnumerable<Expression<Func<DomainBasicLog, object>>>>(),
                "",
                It.Is<FilterDefinition<DomainBasicLog>>(f => f != null)
            ),
            Times.Once
        );
    }

    [Fact]
    public void GetBasicLogs_WithoutLevels_CallsGetPagedWithNullAdditionalFilter()
    {
        var expectedResult = new PagedResult<DomainBasicLog>(0, []);
        _mockLogContext.Setup(x => x.GetPaged(
                                  It.IsAny<int>(),
                                  It.IsAny<int>(),
                                  It.IsAny<SortDirection>(),
                                  It.IsAny<string>(),
                                  It.IsAny<IEnumerable<Expression<Func<DomainBasicLog, object>>>>(),
                                  It.IsAny<string>(),
                                  It.IsAny<FilterDefinition<DomainBasicLog>>()
                              )
                       )
                       .Returns(expectedResult);

        var result = _controller.GetBasicLogs(1, 10, SortDirection.Descending, "Timestamp", "", null);

        result.Should().BeSameAs(expectedResult);
        _mockLogContext.Verify(
            x => x.GetPaged(
                1,
                10,
                SortDirection.Descending,
                "Timestamp",
                It.IsAny<IEnumerable<Expression<Func<DomainBasicLog, object>>>>(),
                "",
                It.Is<FilterDefinition<DomainBasicLog>>(f => f == null)
            ),
            Times.Once
        );
    }

    [Fact]
    public void GetDiscordLogs_WithEventTypes_CallsGetPagedWithNonNullAdditionalFilter()
    {
        var expectedResult = new PagedResult<DiscordLog>(0, []);
        _mockDiscordLogContext.Setup(x => x.GetPaged(
                                         It.IsAny<int>(),
                                         It.IsAny<int>(),
                                         It.IsAny<SortDirection>(),
                                         It.IsAny<string>(),
                                         It.IsAny<IEnumerable<Expression<Func<DiscordLog, object>>>>(),
                                         It.IsAny<string>(),
                                         It.IsAny<FilterDefinition<DiscordLog>>()
                                     )
                              )
                              .Returns(expectedResult);

        var result = _controller.GetDiscordLogs(1, 10, SortDirection.Descending, "Timestamp", "", "Joined,Left");

        result.Should().BeSameAs(expectedResult);
        _mockDiscordLogContext.Verify(
            x => x.GetPaged(
                1,
                10,
                SortDirection.Descending,
                "Timestamp",
                It.IsAny<IEnumerable<Expression<Func<DiscordLog, object>>>>(),
                "",
                It.Is<FilterDefinition<DiscordLog>>(f => f != null)
            ),
            Times.Once
        );
    }

    [Fact]
    public void GetDiscordLogs_WithoutEventTypes_CallsGetPagedWithNullAdditionalFilter()
    {
        var expectedResult = new PagedResult<DiscordLog>(0, []);
        _mockDiscordLogContext.Setup(x => x.GetPaged(
                                         It.IsAny<int>(),
                                         It.IsAny<int>(),
                                         It.IsAny<SortDirection>(),
                                         It.IsAny<string>(),
                                         It.IsAny<IEnumerable<Expression<Func<DiscordLog, object>>>>(),
                                         It.IsAny<string>(),
                                         It.IsAny<FilterDefinition<DiscordLog>>()
                                     )
                              )
                              .Returns(expectedResult);

        var result = _controller.GetDiscordLogs(1, 10, SortDirection.Descending, "Timestamp", "", null);

        result.Should().BeSameAs(expectedResult);
        _mockDiscordLogContext.Verify(
            x => x.GetPaged(
                1,
                10,
                SortDirection.Descending,
                "Timestamp",
                It.IsAny<IEnumerable<Expression<Func<DiscordLog, object>>>>(),
                "",
                It.Is<FilterDefinition<DiscordLog>>(f => f == null)
            ),
            Times.Once
        );
    }
}
