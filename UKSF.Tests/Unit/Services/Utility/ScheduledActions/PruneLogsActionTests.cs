using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility.ScheduledActions;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Services.Utility.ScheduledActions;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Services.Utility.ScheduledActions {
    public class PruneLogsActionTests {
        private readonly Mock<IDataCollectionFactory> mockDataCollectionFactory;
        private IPruneLogsAction pruneLogsAction;

        public PruneLogsActionTests() => mockDataCollectionFactory = new Mock<IDataCollectionFactory>();

        [Fact]
        public void ShouldRemoveOldLogsAndNotifications() {
            List<BasicLogMessage> mockBasicLogMessageCollection = new List<BasicLogMessage> {
                new BasicLogMessage("test1"), new BasicLogMessage("test2") { timestamp = DateTime.Now.AddDays(-10) }, new BasicLogMessage("test3") { timestamp = DateTime.Now.AddDays(-6) }
            };
            List<WebLogMessage> mockWebLogMessageCollection = new List<WebLogMessage> {
                new WebLogMessage(new Exception("error1")),
                new WebLogMessage(new Exception("error2")) { timestamp = DateTime.Now.AddDays(-10) },
                new WebLogMessage(new Exception("error3")) { timestamp = DateTime.Now.AddDays(-6) }
            };
            List<AuditLogMessage> mockAuditLogMessageCollection = new List<AuditLogMessage> {
                new AuditLogMessage { message = "audit1" },
                new AuditLogMessage { message = "audit2", timestamp = DateTime.Now.AddDays(-100) },
                new AuditLogMessage { message = "audit3", timestamp = DateTime.Now.AddMonths(-2) }
            };
            List<Notification> mockNotificationCollection = new List<Notification> {
                new Notification { message = "notification1" },
                new Notification { message = "notification2", timestamp = DateTime.Now.AddDays(-40) },
                new Notification { message = "notification3", timestamp = DateTime.Now.AddDays(-25) }
            };

            Mock<IDataCollection<BasicLogMessage>> mockBasicLogMessageDataColection = new Mock<IDataCollection<BasicLogMessage>>();
            Mock<IDataCollection<WebLogMessage>> mockWebLogMessageDataColection = new Mock<IDataCollection<WebLogMessage>>();
            Mock<IDataCollection<AuditLogMessage>> mockAuditLogMessageDataColection = new Mock<IDataCollection<AuditLogMessage>>();
            Mock<IDataCollection<Notification>> mockNotificationDataColection = new Mock<IDataCollection<Notification>>();

            mockBasicLogMessageDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<BasicLogMessage, bool>>>()))
                                            .Callback<Expression<Func<BasicLogMessage, bool>>>(x => mockBasicLogMessageCollection.RemoveAll(y => x.Compile()(y)));
            mockWebLogMessageDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<WebLogMessage, bool>>>()))
                                          .Callback<Expression<Func<WebLogMessage, bool>>>(x => mockWebLogMessageCollection.RemoveAll(y => x.Compile()(y)));
            mockAuditLogMessageDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<AuditLogMessage, bool>>>()))
                                            .Callback<Expression<Func<AuditLogMessage, bool>>>(x => mockAuditLogMessageCollection.RemoveAll(y => x.Compile()(y)));
            mockNotificationDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<Notification, bool>>>()))
                                         .Callback<Expression<Func<Notification, bool>>>(x => mockNotificationCollection.RemoveAll(y => x.Compile()(y)));

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<BasicLogMessage>("logs")).Returns(mockBasicLogMessageDataColection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<WebLogMessage>("errorLogs")).Returns(mockWebLogMessageDataColection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<AuditLogMessage>("auditLogs")).Returns(mockAuditLogMessageDataColection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Notification>("notifications")).Returns(mockNotificationDataColection.Object);

            pruneLogsAction = new PruneLogsAction(mockDataCollectionFactory.Object);

            pruneLogsAction.Run();

            mockBasicLogMessageCollection.Should().NotContain(x => x.message == "test2");
            mockWebLogMessageCollection.Should().NotContain(x => x.message == "error2");
            mockAuditLogMessageCollection.Should().NotContain(x => x.message == "audit2");
            mockNotificationCollection.Should().NotContain(x => x.message == "notification2");
        }

        [Fact]
        public void ShouldReturnActionName() {
            pruneLogsAction = new PruneLogsAction(mockDataCollectionFactory.Object);

            string subject = pruneLogsAction.Name;

            subject.Should().Be("PruneLogsAction");
        }
    }
}
