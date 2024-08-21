using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
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

        AccountContext unused1 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object, new Mock<IVariablesService>().Object);
        CommandRequestContext unused2 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object, new Mock<IVariablesService>().Object);
        CommandRequestArchiveContext unused3 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);
        ConfirmationCodeContext unused4 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);
        LauncherFileContext unused5 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object, new Mock<IVariablesService>().Object);
        LoaContext unused6 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object, new Mock<IVariablesService>().Object);
        NotificationsContext unused7 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object, new Mock<IVariablesService>().Object);
        SchedulerContext unused8 = new(mockDataCollectionFactory.Object, new Mock<IEventBus>().Object);

        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<DomainAccount>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<DomainCommandRequest>(It.IsAny<string>()), Times.Exactly(2));
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<DomainConfirmationCode>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<LauncherFile>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<DomainLoa>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<DomainNotification>(It.IsAny<string>()), Times.Once);
        mockDataCollectionFactory.Verify(x => x.CreateMongoCollection<DomainScheduledJob>(It.IsAny<string>()), Times.Once);
    }
}
