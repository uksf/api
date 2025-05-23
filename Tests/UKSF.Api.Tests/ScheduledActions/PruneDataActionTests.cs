using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.ScheduledActions;
using Xunit;

namespace UKSF.Api.Tests.ScheduledActions;

public class PruneDataActionTests
{
    private readonly IActionPruneLogs _actionPruneLogs;
    private readonly Mock<IAuditLogContext> _mockAuditLogContext = new();
    private readonly Mock<IErrorLogContext> _mockErrorLogContext = new();
    private readonly Mock<ILogContext> _mockLogContext = new();
    private readonly DateTime _now;

    public PruneDataActionTests()
    {
        Mock<IClock> mockClock = new();
        Mock<IHostEnvironment> mockHostEnvironment = new();
        Mock<ISchedulerService> mockSchedulerService = new();

        _now = new DateTime(2020, 11, 14);
        mockClock.Setup(x => x.UtcNow()).Returns(_now);

        _actionPruneLogs = new ActionPruneLogs(
            _mockLogContext.Object,
            _mockAuditLogContext.Object,
            _mockErrorLogContext.Object,
            mockSchedulerService.Object,
            mockHostEnvironment.Object,
            mockClock.Object
        );
    }

    [Fact]
    public void When_getting_action_name()
    {
        var subject = _actionPruneLogs.Name;

        subject.Should().Be("ActionPruneLogs");
    }

    [Fact]
    public async Task When_pruning_logs()
    {
        List<DomainBasicLog> basicLogs =
            [new DomainBasicLog("test1") { Timestamp = _now.AddDays(-8) }, new DomainBasicLog("test2") { Timestamp = _now.AddDays(-6) }];
        List<AuditLog> auditLogs =
            [new AuditLog("server", "audit1") { Timestamp = _now.AddMonths(-4) }, new AuditLog("server", "audit2") { Timestamp = _now.AddMonths(-2) }];
        List<ErrorLog> errorLogs =
            [new ErrorLog(new Exception("error1")) { Timestamp = _now.AddDays(-8) }, new ErrorLog(new Exception("error2")) { Timestamp = _now.AddDays(-6) }];

        _mockLogContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<DomainBasicLog, bool>>>()))
                       .Returns(Task.CompletedTask)
                       .Callback<Expression<Func<DomainBasicLog, bool>>>(x => basicLogs.RemoveAll(y => x.Compile()(y)));
        _mockAuditLogContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<AuditLog, bool>>>()))
                            .Returns(Task.CompletedTask)
                            .Callback<Expression<Func<AuditLog, bool>>>(x => auditLogs.RemoveAll(y => x.Compile()(y)));
        _mockErrorLogContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<ErrorLog, bool>>>()))
                            .Returns(Task.CompletedTask)
                            .Callback<Expression<Func<ErrorLog, bool>>>(x => errorLogs.RemoveAll(y => x.Compile()(y)));

        await _actionPruneLogs.Run();

        basicLogs.Should().NotContain(x => x.Message == "test1");
        auditLogs.Should().NotContain(x => x.Message == "audit1");
        errorLogs.Should().NotContain(x => x.Message == "error1");
    }
}
