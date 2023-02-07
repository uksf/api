using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Launcher.Context;
using UKSF.Api.Launcher.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data;

public class SimpleDataServiceTests
{
    [Fact]
    public void Should_create_collections()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();

        AccountContext unused1 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);
        CommandRequestContext unused2 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);
        CommandRequestArchiveContext unused3 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);
        ConfirmationCodeContext unused4 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);
        LauncherFileContext unused5 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);
        LoaContext unused6 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);
        NotificationsContext unused7 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);
        SchedulerContext unused8 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);

        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<DomainAccount>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<CommandRequest>(It.IsAny<string>()), Times.Exactly(2));
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<ConfirmationCode>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<LauncherFile>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<DomainLoa>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<Notification>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<ScheduledJob>(It.IsAny<string>()), Times.Once);
    }
}
