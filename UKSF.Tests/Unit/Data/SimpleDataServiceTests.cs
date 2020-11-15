using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Launcher.Context;
using UKSF.Api.Launcher.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data {
    public class SimpleDataServiceTests {
        [Fact]
        public void Should_create_collections() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();

            AccountDataService unused1 = new AccountDataService(mockDataCollectionFactory.Object, new Mock<IDataEventBus<Account>>().Object);
            CommandRequestDataService unused2 = new CommandRequestDataService(mockDataCollectionFactory.Object, new Mock<IDataEventBus<CommandRequest>>().Object);
            CommandRequestArchiveDataService unused3 = new CommandRequestArchiveDataService(mockDataCollectionFactory.Object, new Mock<IDataEventBus<CommandRequest>>().Object);
            ConfirmationCodeDataService unused4 = new ConfirmationCodeDataService(mockDataCollectionFactory.Object, new Mock<IDataEventBus<ConfirmationCode>>().Object);
            LauncherFileDataService unused5 = new LauncherFileDataService(mockDataCollectionFactory.Object, new Mock<IDataEventBus<LauncherFile>>().Object);
            LoaDataService unused6 = new LoaDataService(mockDataCollectionFactory.Object, new Mock<IDataEventBus<Loa>>().Object);
            NotificationsDataService unused7 = new NotificationsDataService(mockDataCollectionFactory.Object, new Mock<IDataEventBus<Notification>>().Object);
            SchedulerDataService unused8 = new SchedulerDataService(mockDataCollectionFactory.Object, new Mock<IDataEventBus<ScheduledJob>>().Object);

            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<Account>(It.IsAny<string>()), Times.Once);
            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<CommandRequest>(It.IsAny<string>()), Times.Exactly(2));
            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<ConfirmationCode>(It.IsAny<string>()), Times.Once);
            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<LauncherFile>(It.IsAny<string>()), Times.Once);
            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<Loa>(It.IsAny<string>()), Times.Once);
            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<Notification>(It.IsAny<string>()), Times.Once);
            mockDataCollectionFactory.Verify(x => x.CreateDataCollection<ScheduledJob>(It.IsAny<string>()), Times.Once);
        }
    }
}
