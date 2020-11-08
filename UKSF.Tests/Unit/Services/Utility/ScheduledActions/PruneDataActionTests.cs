using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Utility.ScheduledActions;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions {
    public class PruneDataActionTests {
        private readonly Mock<IDataCollectionFactory> mockDataCollectionFactory;
        private IPruneDataAction pruneDataAction;

        public PruneDataActionTests() => mockDataCollectionFactory = new Mock<IDataCollectionFactory>();

        [Fact]
        public void ShouldRemoveOldLogsAndNotifications() {
            List<BasicLog> mockBasicLogMessageCollection = new List<BasicLog> {
                new BasicLog("test1"), new BasicLog("test2") { timestamp = DateTime.Now.AddDays(-10) }, new BasicLog("test3") { timestamp = DateTime.Now.AddDays(-6) }
            };
            List<HttpErrorLog> mockWebLogMessageCollection = new List<HttpErrorLog> {
                new HttpErrorLog(new Exception("error1")),
                new HttpErrorLog(new Exception("error2")) { timestamp = DateTime.Now.AddDays(-10) },
                new HttpErrorLog(new Exception("error3")) { timestamp = DateTime.Now.AddDays(-6) }
            };
            List<AuditLog> mockAuditLogMessageCollection = new List<AuditLog> {
                new AuditLog("server", "audit1"),
                new AuditLog("server", "audit2") { timestamp = DateTime.Now.AddDays(-100) },
                new AuditLog("server", "audit3") { timestamp = DateTime.Now.AddMonths(-2) }
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

            Mock<IDataCollection<BasicLog>> mockBasicLogMessageDataColection = new Mock<IDataCollection<BasicLog>>();
            Mock<IDataCollection<HttpErrorLog>> mockWebLogMessageDataColection = new Mock<IDataCollection<HttpErrorLog>>();
            Mock<IDataCollection<AuditLog>> mockAuditLogMessageDataColection = new Mock<IDataCollection<AuditLog>>();
            Mock<IDataCollection<Notification>> mockNotificationDataColection = new Mock<IDataCollection<Notification>>();
            Mock<IDataCollection<ModpackBuild>> mockModpackBuildDataColection = new Mock<IDataCollection<ModpackBuild>>();

            mockModpackBuildDataColection.Setup(x => x.Get(It.IsAny<Func<ModpackBuild, bool>>())).Returns<Func<ModpackBuild, bool>>(x => mockModpackBuildCollection.Where(x));

            mockBasicLogMessageDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<BasicLog, bool>>>()))
                                            .Callback<Expression<Func<BasicLog, bool>>>(x => mockBasicLogMessageCollection.RemoveAll(y => x.Compile()(y)));
            mockWebLogMessageDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<HttpErrorLog, bool>>>()))
                                          .Callback<Expression<Func<HttpErrorLog, bool>>>(x => mockWebLogMessageCollection.RemoveAll(y => x.Compile()(y)));
            mockAuditLogMessageDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<AuditLog, bool>>>()))
                                            .Callback<Expression<Func<AuditLog, bool>>>(x => mockAuditLogMessageCollection.RemoveAll(y => x.Compile()(y)));
            mockNotificationDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<Notification, bool>>>()))
                                         .Callback<Expression<Func<Notification, bool>>>(x => mockNotificationCollection.RemoveAll(y => x.Compile()(y)));
            mockModpackBuildDataColection.Setup(x => x.DeleteManyAsync(It.IsAny<Expression<Func<ModpackBuild, bool>>>()))
                                         .Callback<Expression<Func<ModpackBuild, bool>>>(x => mockModpackBuildCollection.RemoveAll(y => x.Compile()(y)));

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<BasicLog>("logs")).Returns(mockBasicLogMessageDataColection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<HttpErrorLog>("errorLogs")).Returns(mockWebLogMessageDataColection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<AuditLog>("auditLogs")).Returns(mockAuditLogMessageDataColection.Object);
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
