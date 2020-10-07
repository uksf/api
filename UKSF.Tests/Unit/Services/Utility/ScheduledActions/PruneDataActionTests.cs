using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Utility.ScheduledActions;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Utility.ScheduledActions;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions {
    public class PruneDataActionTests {
        private readonly Mock<IDataCollectionFactory> mockDataCollectionFactory;
        private IPruneDataAction pruneDataAction;

        public PruneDataActionTests() => mockDataCollectionFactory = new Mock<IDataCollectionFactory>();

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
            List<ModpackBuild> mockModpackBuildCollection = new List<ModpackBuild> {
                new ModpackBuild { environment = GameEnvironment.DEV, buildNumber = 1, version = "5.0.0" },
                new ModpackBuild { environment = GameEnvironment.RC, buildNumber = 1, version = "5.18.0" },
                new ModpackBuild { environment = GameEnvironment.RELEASE, buildNumber = 2, version = "5.18.0" },
                new ModpackBuild { environment = GameEnvironment.DEV, buildNumber = 150, version = "5.19.0" }
            };

            Mock<IDataCollection<BasicLogMessage>> mockBasicLogMessageDataColection = new Mock<IDataCollection<BasicLogMessage>>();
            Mock<IDataCollection<WebLogMessage>> mockWebLogMessageDataColection = new Mock<IDataCollection<WebLogMessage>>();
            Mock<IDataCollection<AuditLogMessage>> mockAuditLogMessageDataColection = new Mock<IDataCollection<AuditLogMessage>>();
            Mock<IDataCollection<Notification>> mockNotificationDataColection = new Mock<IDataCollection<Notification>>();
            Mock<IDataCollection<ModpackBuild>> mockModpackBuildDataColection = new Mock<IDataCollection<ModpackBuild>>();

            mockModpackBuildDataColection.Setup(x => x.Get(It.IsAny<Func<ModpackBuild, bool>>())).Returns<Func<ModpackBuild, bool>>(x => mockModpackBuildCollection.Where(x));

            mockBasicLogMessageDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<BasicLogMessage, bool>>>()))
                                            .Callback<Expression<Func<BasicLogMessage, bool>>>(x => mockBasicLogMessageCollection.RemoveAll(y => x.Compile()(y)));
            mockWebLogMessageDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<WebLogMessage, bool>>>()))
                                          .Callback<Expression<Func<WebLogMessage, bool>>>(x => mockWebLogMessageCollection.RemoveAll(y => x.Compile()(y)));
            mockAuditLogMessageDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<AuditLogMessage, bool>>>()))
                                            .Callback<Expression<Func<AuditLogMessage, bool>>>(x => mockAuditLogMessageCollection.RemoveAll(y => x.Compile()(y)));
            mockNotificationDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<Notification, bool>>>()))
                                         .Callback<Expression<Func<Notification, bool>>>(x => mockNotificationCollection.RemoveAll(y => x.Compile()(y)));
            mockModpackBuildDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<ModpackBuild, bool>>>()))
                                         .Callback<Expression<Func<ModpackBuild, bool>>>(x => mockModpackBuildCollection.RemoveAll(y => x.Compile()(y)));

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<BasicLogMessage>("logs")).Returns(mockBasicLogMessageDataColection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<WebLogMessage>("errorLogs")).Returns(mockWebLogMessageDataColection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<AuditLogMessage>("auditLogs")).Returns(mockAuditLogMessageDataColection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Notification>("notifications")).Returns(mockNotificationDataColection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<ModpackBuild>("modpackBuilds")).Returns(mockModpackBuildDataColection.Object);

            pruneDataAction = new PruneDataAction(mockDataCollectionFactory.Object);

            pruneDataAction.Run();

            mockBasicLogMessageCollection.Should().NotContain(x => x.message == "test2");
            mockWebLogMessageCollection.Should().NotContain(x => x.message == "error2");
            mockAuditLogMessageCollection.Should().NotContain(x => x.message == "audit2");
            mockNotificationCollection.Should().NotContain(x => x.message == "notification2");
            mockModpackBuildCollection.Should().NotContain(x => x.version == "5.0.0");
            mockModpackBuildCollection.Should().NotContain(x => x.version == "5.18.0");
        }

        [Fact]
        public void ShouldReturnActionName() {
            pruneDataAction = new PruneDataAction(mockDataCollectionFactory.Object);

            string subject = pruneDataAction.Name;

            subject.Should().Be("PruneDataAction");
        }
    }
}
