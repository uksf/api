using System.Diagnostics.Contracts;
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
        private readonly Mock<IDataCollection> mockDataCollection;
        private readonly LogDataService logDataService;

        public LogDataServiceTests() {
            mockDataCollection = new Mock<IDataCollection>();
            Mock<IDataEventBus<ILogDataService>> mockDataEventBus = new Mock<IDataEventBus<ILogDataService>>();

            logDataService = new LogDataService(mockDataCollection.Object, mockDataEventBus.Object);
        }

        [Fact]
        public async Task ShouldUseAuditLogCollection() {
            AuditLogMessage logMessage = new AuditLogMessage();
            string subject = "";

            mockDataCollection.Setup(x => x.Add(It.IsAny<string>(), It.IsAny<BasicLogMessage>())).Returns(Task.CompletedTask).Callback<string, BasicLogMessage>((collectionName, _) => subject = collectionName);

            await logDataService.Add(logMessage);

            subject.Should().Be("auditLogs");
        }

        [Fact]
        public async Task ShouldUseLauncherLogCollection() {
            LauncherLogMessage logMessage = new LauncherLogMessage("1", "test");
            string subject = "";

            mockDataCollection.Setup(x => x.Add(It.IsAny<string>(), It.IsAny<BasicLogMessage>())).Returns(Task.CompletedTask).Callback<string, BasicLogMessage>((collectionName, _) => subject = collectionName);

            await logDataService.Add(logMessage);

            subject.Should().Be("launcherLogs");
        }

        [Fact]
        public async Task ShouldUseErrorLogCollection() {
            WebLogMessage logMessage = new WebLogMessage();
            string subject = "";

            mockDataCollection.Setup(x => x.Add(It.IsAny<string>(), It.IsAny<BasicLogMessage>())).Returns(Task.CompletedTask).Callback<string, BasicLogMessage>((collectionName, _) => subject = collectionName);

            await logDataService.Add(logMessage);

            subject.Should().Be("errorLogs");
        }
    }
}
