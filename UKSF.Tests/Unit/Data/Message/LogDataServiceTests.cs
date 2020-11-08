using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.Base.Services.Data;
using Xunit;

namespace UKSF.Tests.Unit.Data.Message {
    public class LogDataServiceTests {
        private readonly LogDataService logDataService;
        private readonly List<AuditLog> mockAuditCollection;
        private readonly List<BasicLog> mockBasicCollection;
        private readonly List<HttpErrorLog> mockErrorCollection;
        private readonly List<LauncherLog> mockLauncherCollection;

        public LogDataServiceTests() {
            Mock<IDataEventBus<BasicLog>> mockDataEventBus = new Mock<IDataEventBus<BasicLog>>();
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();

            Mock<IDataCollection<BasicLog>> mockBasicDataCollection = new Mock<IDataCollection<BasicLog>>();
            Mock<IDataCollection<AuditLog>> mockAuditDataCollection = new Mock<IDataCollection<AuditLog>>();
            Mock<IDataCollection<LauncherLog>> mockLauncherDataCollection = new Mock<IDataCollection<LauncherLog>>();
            Mock<IDataCollection<HttpErrorLog>> mockErrorDataCollection = new Mock<IDataCollection<HttpErrorLog>>();

            mockBasicCollection = new List<BasicLog>();
            mockAuditCollection = new List<AuditLog>();
            mockLauncherCollection = new List<LauncherLog>();
            mockErrorCollection = new List<HttpErrorLog>();

            mockBasicDataCollection.Setup(x => x.AddAsync(It.IsAny<BasicLog>())).Returns(Task.CompletedTask).Callback<BasicLog>(x => mockBasicCollection.Add(x));
            mockAuditDataCollection.Setup(x => x.AddAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask).Callback<AuditLog>(x => mockAuditCollection.Add(x));
            mockLauncherDataCollection.Setup(x => x.AddAsync(It.IsAny<LauncherLog>())).Returns(Task.CompletedTask).Callback<LauncherLog>(x => mockLauncherCollection.Add(x));
            mockErrorDataCollection.Setup(x => x.AddAsync(It.IsAny<HttpErrorLog>())).Returns(Task.CompletedTask).Callback<HttpErrorLog>(x => mockErrorCollection.Add(x));

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<BasicLog>("logs")).Returns(mockBasicDataCollection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<AuditLog>("auditLogs")).Returns(mockAuditDataCollection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<LauncherLog>("launcherLogs")).Returns(mockLauncherDataCollection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<HttpErrorLog>("errorLogs")).Returns(mockErrorDataCollection.Object);

            logDataService = new LogDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public async Task ShouldUseAuditLogCollection() {
            AuditLog logMessage = new AuditLog("server", "test");

            await logDataService.Add(logMessage);

            mockBasicCollection.Should().BeEmpty();
            mockAuditCollection.Should().ContainSingle().And.Contain(logMessage);
            mockLauncherCollection.Should().BeEmpty();
            mockErrorCollection.Should().BeEmpty();
        }

        [Fact]
        public async Task ShouldUseCorrectCollection() {
            BasicLog basicLogMessage = new BasicLog("test");
            AuditLog auditLogMessage = new AuditLog("server", "test");
            LauncherLog launcherLogMessage = new LauncherLog("1", "test");
            HttpErrorLog webLogMessage = new HttpErrorLog();

            await logDataService.Add(basicLogMessage);
            await logDataService.Add(auditLogMessage);
            await logDataService.Add(launcherLogMessage);
            await logDataService.Add(webLogMessage);

            mockBasicCollection.Should().ContainSingle().And.Contain(basicLogMessage);
            mockAuditCollection.Should().ContainSingle().And.Contain(auditLogMessage);
            mockLauncherCollection.Should().ContainSingle().And.Contain(launcherLogMessage);
            mockErrorCollection.Should().ContainSingle().And.Contain(webLogMessage);
        }

        [Fact]
        public async Task ShouldUseErrorLogCollection() {
            HttpErrorLog logMessage = new HttpErrorLog();

            await logDataService.Add(logMessage);

            mockBasicCollection.Should().BeEmpty();
            mockAuditCollection.Should().BeEmpty();
            mockLauncherCollection.Should().BeEmpty();
            mockErrorCollection.Should().ContainSingle().And.Contain(logMessage);
        }

        [Fact]
        public async Task ShouldUseLauncherLogCollection() {
            LauncherLog logMessage = new LauncherLog("1", "test");

            await logDataService.Add(logMessage);

            mockBasicCollection.Should().BeEmpty();
            mockAuditCollection.Should().BeEmpty();
            mockLauncherCollection.Should().ContainSingle().And.Contain(logMessage);
            mockErrorCollection.Should().BeEmpty();
        }

        [Fact]
        public async Task ShouldUseLogCollection() {
            BasicLog logMessage = new BasicLog("test");

            await logDataService.Add(logMessage);

            mockBasicCollection.Should().ContainSingle().And.Contain(logMessage);
            mockAuditCollection.Should().BeEmpty();
            mockLauncherCollection.Should().BeEmpty();
            mockErrorCollection.Should().BeEmpty();
        }
    }
}
