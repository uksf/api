using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Modpack.EventHandlers;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Signalr.Clients;
using UKSF.Api.Modpack.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Modpack.Tests.EventHandlers;

public class WorkshopModDataEventHandlerTests
{
    private readonly WorkshopModDataEventHandler _subject;
    private readonly IEventBus _eventBus;
    private readonly Mock<IHubContext<ModpackHub, IModpackClient>> _mockHub;
    private readonly Mock<IModpackClient> _mockClient;

    public WorkshopModDataEventHandlerTests()
    {
        Mock<IUksfLogger> mockLogger = new();
        _mockHub = new Mock<IHubContext<ModpackHub, IModpackClient>>();
        _eventBus = new EventBus();

        Mock<IHubClients<IModpackClient>> mockHubClients = new();
        _mockClient = new Mock<IModpackClient>();
        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.All).Returns(_mockClient.Object);

        _subject = new WorkshopModDataEventHandler(_eventBus, _mockHub.Object, mockLogger.Object);
    }

    [Fact]
    public void ShouldCallModListChangedOnAdd()
    {
        _subject.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainWorkshopMod>("1", new DomainWorkshopMod()), ""));

        _mockClient.Verify(x => x.ReceiveWorkshopModAdded(), Times.Once);
        _mockClient.Verify(x => x.ReceiveWorkshopModUpdate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ShouldCallModListChangedOnDelete()
    {
        _subject.Init();

        _eventBus.Send(new EventModel(EventType.Delete, new ContextEventData<DomainWorkshopMod>("1", new DomainWorkshopMod()), ""));

        _mockClient.Verify(x => x.ReceiveWorkshopModAdded(), Times.Once);
        _mockClient.Verify(x => x.ReceiveWorkshopModUpdate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ShouldCallUpdateOnUpdate()
    {
        _subject.Init();

        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainWorkshopMod>("test-id", new DomainWorkshopMod()), ""));

        _mockClient.Verify(x => x.ReceiveWorkshopModUpdate("test-id"), Times.Once);
        _mockClient.Verify(x => x.ReceiveWorkshopModAdded(), Times.Never);
    }

    [Fact]
    public void ShouldNotCallAnyHandlerBeforeInit()
    {
        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainWorkshopMod>("1", new DomainWorkshopMod()), ""));

        _mockClient.Verify(x => x.ReceiveWorkshopModAdded(), Times.Never);
        _mockClient.Verify(x => x.ReceiveWorkshopModUpdate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void EarlyInit_ShouldNotThrow()
    {
        var act = () => _subject.EarlyInit();

        act.Should().NotThrow();
    }
}
