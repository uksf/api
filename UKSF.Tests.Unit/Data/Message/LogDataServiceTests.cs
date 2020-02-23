using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Data.Message;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Message.Logging;
using Xunit;

namespace UKSF.Tests.Unit.Data.Message {
    public class LogDataServiceTests {
        private readonly List<BasicLogMessage> mockBasicCollection;
        private readonly List<AuditLogMessage> mockAuditCollection;
        private readonly List<LauncherLogMessage> mockLauncherCollection;
        private readonly List<WebLogMessage> mockErrorCollection;
        private readonly LogDataService logDataService;

        public LogDataServiceTests() {
            Mock<IDataEventBus<ILogDataService>> mockDataEventBus = new Mock<IDataEventBus<ILogDataService>>();
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();

            Mock<IDataCollection> mockBasicDataCollection = new Mock<IDataCollection>();
            Mock<IDataCollection> mockAuditDataCollection = new Mock<IDataCollection>();
            Mock<IDataCollection> mockLauncherDataCollection = new Mock<IDataCollection>();
            Mock<IDataCollection> mockErrorDataCollection = new Mock<IDataCollection>();

            mockBasicCollection = new List<BasicLogMessage>();
            mockAuditCollection = new List<AuditLogMessage>();
            mockLauncherCollection = new List<LauncherLogMessage>();
            mockErrorCollection = new List<WebLogMessage>();

            mockBasicDataCollection.Setup(x => x.AddAsync(It.IsAny<BasicLogMessage>())).Returns(Task.CompletedTask).Callback<BasicLogMessage>(x => mockBasicCollection.Add(x));

            mockAuditDataCollection.Setup(x => x.AddAsync(It.IsAny<AuditLogMessage>())).Returns(Task.CompletedTask).Callback<AuditLogMessage>(x => mockAuditCollection.Add(x));

            mockLauncherDataCollection.Setup(x => x.AddAsync(It.IsAny<LauncherLogMessage>())).Returns(Task.CompletedTask).Callback<LauncherLogMessage>(x => mockLauncherCollection.Add(x));

            mockErrorDataCollection.Setup(x => x.AddAsync(It.IsAny<WebLogMessage>())).Returns(Task.CompletedTask).Callback<WebLogMessage>(x => mockErrorCollection.Add(x));

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection("logs")).Returns(mockBasicDataCollection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection("auditLogs")).Returns(mockAuditDataCollection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection("launcherLogs")).Returns(mockLauncherDataCollection.Object);
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection("errorLogs")).Returns(mockErrorDataCollection.Object);

            logDataService = new LogDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);
        }

        [Fact]
        public async Task ShouldUseLogCollection() {
            BasicLogMessage logMessage = new BasicLogMessage("test");

            await logDataService.Add(logMessage);

            mockBasicCollection.Should().ContainSingle().And.Contain(logMessage);
            mockAuditCollection.Should().BeEmpty();
            mockLauncherCollection.Should().BeEmpty();
            mockErrorCollection.Should().BeEmpty();
        }

        [Fact]
        public async Task ShouldUseAuditLogCollection() {
            AuditLogMessage logMessage = new AuditLogMessage();

            await logDataService.Add(logMessage);

            mockBasicCollection.Should().BeEmpty();
            mockAuditCollection.Should().ContainSingle().And.Contain(logMessage);
            mockLauncherCollection.Should().BeEmpty();
            mockErrorCollection.Should().BeEmpty();
        }

        [Fact]
        public async Task ShouldUseLauncherLogCollection() {
            LauncherLogMessage logMessage = new LauncherLogMessage("1", "test");

            await logDataService.Add(logMessage);

            mockBasicCollection.Should().BeEmpty();
            mockAuditCollection.Should().BeEmpty();
            mockLauncherCollection.Should().ContainSingle().And.Contain(logMessage);
            mockErrorCollection.Should().BeEmpty();
        }

        [Fact]
        public async Task ShouldUseErrorLogCollection() {
            WebLogMessage logMessage = new WebLogMessage();

            await logDataService.Add(logMessage);

            mockBasicCollection.Should().BeEmpty();
            mockAuditCollection.Should().BeEmpty();
            mockLauncherCollection.Should().BeEmpty();
            mockErrorCollection.Should().ContainSingle().And.Contain(logMessage);
        }

        [Fact]
        public async Task ShouldUseCorrectCollection() {
            BasicLogMessage basicLogMessage = new BasicLogMessage("test");
            AuditLogMessage auditLogMessage = new AuditLogMessage();
            LauncherLogMessage launcherLogMessage = new LauncherLogMessage("1", "test");
            WebLogMessage webLogMessage = new WebLogMessage();

            await logDataService.Add(basicLogMessage);
            await logDataService.Add(auditLogMessage);
            await logDataService.Add(launcherLogMessage);
            await logDataService.Add(webLogMessage);

            mockBasicCollection.Should().ContainSingle().And.Contain(basicLogMessage);
            mockAuditCollection.Should().ContainSingle().And.Contain(auditLogMessage);
            mockLauncherCollection.Should().ContainSingle().And.Contain(launcherLogMessage);
            mockErrorCollection.Should().ContainSingle().And.Contain(webLogMessage);
        }
    }
}
