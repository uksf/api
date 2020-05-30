using Moq;
using UKSF.Api.Data.Command;
using UKSF.Api.Data.Launcher;
using UKSF.Api.Data.Personnel;
using UKSF.Api.Data.Utility;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Launcher;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Utility;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Data {
    public class SimpleDataServiceTests {
        private readonly Mock<IDataCollectionFactory> mockDataCollectionFactory;

        public SimpleDataServiceTests() => mockDataCollectionFactory = new Mock<IDataCollectionFactory>();

        [Fact]
        public void ShouldCreateConfirmationCodeDataCollection() {
            Mock<IDataEventBus<IConfirmationCodeDataService>> mockDataEventBus = new Mock<IDataEventBus<IConfirmationCodeDataService>>();

            ConfirmationCodeDataService unused = new ConfirmationCodeDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<ConfirmationCode>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ShouldCreateSchedulerDataCollection() {
            Mock<IDataEventBus<ISchedulerDataService>> mockDataEventBus = new Mock<IDataEventBus<ISchedulerDataService>>();

            SchedulerDataService unused = new SchedulerDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<ScheduledJob>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ShouldCreateCommandRequestArchiveDataCollection() {
            Mock<IDataEventBus<ICommandRequestArchiveDataService>> mockDataEventBus = new Mock<IDataEventBus<ICommandRequestArchiveDataService>>();

            CommandRequestArchiveDataService unused = new CommandRequestArchiveDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<CommandRequest>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ShouldCreateLoaDataCollection() {
            Mock<IDataEventBus<ILoaDataService>> mockDataEventBus = new Mock<IDataEventBus<ILoaDataService>>();

            LoaDataService unused = new LoaDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<Loa>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ShouldCreateLauncherFileDataCollection() {
            Mock<IDataEventBus<ILauncherFileDataService>> mockDataEventBus = new Mock<IDataEventBus<ILauncherFileDataService>>();

            LauncherFileDataService unused = new LauncherFileDataService(mockDataCollectionFactory.Object, mockDataEventBus.Object);

            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<LauncherFile>(It.IsAny<string>()), Times.Once);
        }
    }
}
