using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Integrations.Teamspeak.EventHandlers;
using UKSF.Api.Integrations.Teamspeak.Models;
using UKSF.Api.Integrations.Teamspeak.Services;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Events.Handlers;

public class TeamspeakEventHandlerTests
{
    private readonly IEventBus _eventBus;
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IUksfLogger> _mockLoggingService;
    private readonly Mock<ITeamspeakGroupService> _mockTeamspeakGroupService;
    private readonly Mock<ITeamspeakService> _mockTeamspeakService;
    private readonly TeamspeakServerEventHandler _teamspeakServerEventHandler;

    public TeamspeakEventHandlerTests()
    {
        _eventBus = new EventBus();
        _mockAccountContext = new Mock<IAccountContext>();
        _mockTeamspeakService = new Mock<ITeamspeakService>();
        _mockTeamspeakGroupService = new Mock<ITeamspeakGroupService>();
        _mockLoggingService = new Mock<IUksfLogger>();

        _teamspeakServerEventHandler = new TeamspeakServerEventHandler(
            _mockAccountContext.Object,
            _eventBus,
            _mockTeamspeakService.Object,
            _mockTeamspeakGroupService.Object,
            _mockLoggingService.Object
        );
    }

    [Fact]
    public void LogOnException()
    {
        _teamspeakServerEventHandler.Init();

        _eventBus.Send(new SignalrEventData { Procedure = (TeamspeakEventType)9 }, "");

        _mockLoggingService.Verify(x => x.LogError(It.IsAny<ArgumentException>()), Times.Once);
    }

    [Fact]
    public void ShouldCorrectlyParseClients()
    {
        HashSet<TeamspeakClient> subject = [];
        _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>())).Callback((HashSet<TeamspeakClient> x) => subject = x);

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(
            new SignalrEventData
            {
                Procedure = TeamspeakEventType.Clients,
                Args = "[{\"channelId\": 1, \"channelName\": \"Test Channel 1\", \"clientDbId\": 5, \"clientName\": \"Test Name 1\"}," +
                       "{\"channelId\": 2, \"channelName\": \"Test Channel 2\", \"clientDbId\": 10, \"clientName\": \"Test Name 2\"}]"
            },
            ""
        );

        subject.Should().HaveCount(2);
        subject.Should()
        .BeEquivalentTo(
            new HashSet<TeamspeakClient>
            {
                new()
                {
                    ChannelId = 1,
                    ChannelName = "Test Channel 1",
                    ClientDbId = 5,
                    ClientName = "Test Name 1"
                },
                new()
                {
                    ChannelId = 2,
                    ChannelName = "Test Channel 2",
                    ClientDbId = 10,
                    ClientName = "Test Name 2"
                }
            }
        );
    }

    [Fact]
    public async Task ShouldGetCorrectAccount()
    {
        DomainAccount account1 = new() { TeamspeakIdentities = [1], MembershipState = MembershipState.Unconfirmed };
        DomainAccount account2 = new() { TeamspeakIdentities = [1], MembershipState = MembershipState.Member };
        List<DomainAccount> mockAccountCollection = [account1, account2];

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns<Func<DomainAccount, bool>>(_ => mockAccountCollection);
        _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(account2, It.IsAny<ICollection<int>>(), 1)).Returns(Task.CompletedTask);

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Client_Server_Groups, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" }, "");
        await Task.Delay(TimeSpan.FromMilliseconds(750));

        _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account2, new List<int> { 5 }, 1), Times.Once);
    }

    [Fact]
    public async Task ShouldGetNoAccountForNoMatchingId()
    {
        DomainAccount account = new() { TeamspeakIdentities = [2] };
        List<DomainAccount> mockCollection = [account];

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns<Func<DomainAccount, bool>>(x => mockCollection.Where(x));
        _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<DomainAccount>(), It.IsAny<ICollection<int>>(), It.IsAny<int>()))
                                  .Returns(Task.CompletedTask);

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Client_Server_Groups, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" }, "");
        await Task.Delay(TimeSpan.FromMilliseconds(750));

        _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(null, new List<int> { 5 }, 1), Times.Once);
    }

    [Fact]
    public void ShouldNotRunEventOnEmpty()
    {
        _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));
        _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<DomainAccount>(), It.IsAny<ICollection<int>>(), It.IsAny<int>()));

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Empty }, "");

        _mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Never);
        _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(It.IsAny<DomainAccount>(), It.IsAny<ICollection<int>>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void ShouldNotRunUpdateClientsForNoClients()
    {
        _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Clients, Args = "[]" }, "");

        _mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Never);
    }

    [Fact]
    public async Task ShouldRunClientGroupsUpdate()
    {
        DomainAccount account = new() { TeamspeakIdentities = [1] };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount> { account });
        _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(account, It.IsAny<ICollection<int>>(), 1)).Returns(Task.CompletedTask);

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Client_Server_Groups, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" }, "");
        await Task.Delay(TimeSpan.FromMilliseconds(750));

        _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<int> { 5 }, 1), Times.Once);
    }

    [Fact]
    public async Task ShouldRunClientGroupsUpdateTwiceForTwoEventsWithDelay()
    {
        DomainAccount account = new() { TeamspeakIdentities = [1] };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount> { account });
        _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<DomainAccount>(), It.IsAny<ICollection<int>>(), It.IsAny<int>()));

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Client_Server_Groups, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" }, "");
        await Task.Delay(TimeSpan.FromMilliseconds(750));
        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Client_Server_Groups, Args = "{\"clientDbid\": 1, \"serverGroupId\": 10}" }, "");
        await Task.Delay(TimeSpan.FromMilliseconds(750));

        _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<int> { 5 }, 1), Times.Once);
        _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<int> { 10 }, 1), Times.Once);
    }

    [Fact]
    public async Task ShouldRunSingleClientGroupsUpdateForEachClient()
    {
        DomainAccount account1 = new() { TeamspeakIdentities = [1] };
        DomainAccount account2 = new() { TeamspeakIdentities = [2] };
        List<DomainAccount> mockCollection = [account1, account2];

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns<Func<DomainAccount, bool>>(x => mockCollection.Where(x));
        _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<DomainAccount>(), It.IsAny<ICollection<int>>(), It.IsAny<int>()));

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Client_Server_Groups, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" }, "");
        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Client_Server_Groups, Args = "{\"clientDbid\": 2, \"serverGroupId\": 10}" }, "");
        await Task.Delay(TimeSpan.FromMilliseconds(750 * 2));

        _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account1, new List<int> { 5 }, 1), Times.Once);
        _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account2, new List<int> { 10 }, 2), Times.Once);
    }

    [Fact]
    public async Task ShouldRunSingleClientGroupsUpdateForMultipleEventsWithOneClient()
    {
        DomainAccount account = new() { TeamspeakIdentities = [1] };

        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount> { account });
        _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<DomainAccount>(), It.IsAny<ICollection<int>>(), It.IsAny<int>()));

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Client_Server_Groups, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" }, "");
        _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.Client_Server_Groups, Args = "{\"clientDbid\": 1, \"serverGroupId\": 10}" }, "");
        await Task.Delay(TimeSpan.FromMilliseconds(750 * 2));

        _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<int> { 5, 10 }, 1), Times.Once);
    }

    [Fact]
    public void ShouldRunUpdateClients()
    {
        _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));

        _teamspeakServerEventHandler.Init();

        _eventBus.Send(
            new SignalrEventData
            {
                Procedure = TeamspeakEventType.Clients,
                Args = "[{\"channelId\": 1, \"channelName\": \"Test Channel\", \"clientDbId\": 5, \"clientName\": \"Test Name\"}]"
            },
            ""
        );

        _mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Once);
    }
}
