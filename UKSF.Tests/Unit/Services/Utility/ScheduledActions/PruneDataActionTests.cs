using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.Admin.ScheduledActions;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions {
    public class PruneDataActionTests {
        private readonly IActionPruneLogs _actionPruneLogs;
        private readonly Mock<IAuditLogDataService> _mockAuditLogDataService;
        private readonly Mock<IHttpErrorLogDataService> _mockHttpErrorLogDataService;
        private readonly Mock<ILogDataService> _mockLogDataService;
        private readonly DateTime _now;

        public PruneDataActionTests() {
            _mockLogDataService = new Mock<ILogDataService>();
            _mockAuditLogDataService = new Mock<IAuditLogDataService>();
            _mockHttpErrorLogDataService = new Mock<IHttpErrorLogDataService>();
            Mock<IClock> mockClock = new Mock<IClock>();
            Mock<IHostEnvironment> mockHostEnvironment = new Mock<IHostEnvironment>();
            Mock<ISchedulerService> mockSchedulerService = new Mock<ISchedulerService>();

            _now = new DateTime(2020, 11, 14);
            mockClock.Setup(x => x.UtcNow()).Returns(_now);

            _actionPruneLogs = new ActionPruneLogs(
                _mockLogDataService.Object,
                _mockAuditLogDataService.Object,
                _mockHttpErrorLogDataService.Object,
                mockSchedulerService.Object,
                mockHostEnvironment.Object,
                mockClock.Object
            );
        }

        [Fact]
        public void When_getting_action_name() {
            string subject = _actionPruneLogs.Name;

            subject.Should().Be("ActionPruneLogs");
        }

        [Fact]
        public void When_pruning_logs() {
            List<BasicLog> basicLogs = new List<BasicLog> { new BasicLog("test1") { timestamp = _now.AddDays(-8) }, new BasicLog("test2") { timestamp = _now.AddDays(-6) } };
            List<AuditLog> auditLogs = new List<AuditLog> { new AuditLog("server", "audit1") { timestamp = _now.AddMonths(-4) }, new AuditLog("server", "audit2") { timestamp = _now.AddMonths(-2) } };
            List<HttpErrorLog> httpErrorLogs = new List<HttpErrorLog> {
                new HttpErrorLog(new Exception("error1")) { timestamp = _now.AddDays(-8) }, new HttpErrorLog(new Exception("error2")) { timestamp = _now.AddDays(-6) }
            };

            _mockLogDataService.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<BasicLog, bool>>>()))
                               .Returns(Task.CompletedTask)
                               .Callback<Expression<Func<BasicLog, bool>>>(x => basicLogs.RemoveAll(y => x.Compile()(y)));
            _mockAuditLogDataService.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<AuditLog, bool>>>()))
                                    .Returns(Task.CompletedTask)
                                    .Callback<Expression<Func<AuditLog, bool>>>(x => auditLogs.RemoveAll(y => x.Compile()(y)));
            _mockHttpErrorLogDataService.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<HttpErrorLog, bool>>>()))
                                        .Returns(Task.CompletedTask)
                                        .Callback<Expression<Func<HttpErrorLog, bool>>>(x => httpErrorLogs.RemoveAll(y => x.Compile()(y)));

            _actionPruneLogs.Run();

            basicLogs.Should().NotContain(x => x.message == "test1");
            auditLogs.Should().NotContain(x => x.message == "audit1");
            httpErrorLogs.Should().NotContain(x => x.message == "error1");
        }
    }
}
