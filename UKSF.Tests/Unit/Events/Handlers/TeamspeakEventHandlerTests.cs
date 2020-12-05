using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.EventHandlers;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Services;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class TeamspeakEventHandlerTests {
        private readonly Mock<IAccountContext> _mockAccountContext;
        private readonly Mock<ILogger> _mockLoggingService;
        private readonly Mock<ITeamspeakGroupService> _mockTeamspeakGroupService;
        private readonly Mock<ITeamspeakService> _mockTeamspeakService;
        private readonly IEventBus _eventBus;
        private readonly TeamspeakServerEventHandler _teamspeakServerEventHandler;

        public TeamspeakEventHandlerTests() {
            _eventBus = new EventBus();
            _mockAccountContext = new Mock<IAccountContext>();
            _mockTeamspeakService = new Mock<ITeamspeakService>();
            _mockTeamspeakGroupService = new Mock<ITeamspeakGroupService>();
            _mockLoggingService = new Mock<ILogger>();

            _teamspeakServerEventHandler = new TeamspeakServerEventHandler(
                _mockAccountContext.Object,
                _eventBus,
                _mockTeamspeakService.Object,
                _mockTeamspeakGroupService.Object,
                _mockLoggingService.Object
            );
        }

        [Theory, InlineData(2), InlineData(-1)]
        public async Task ShouldGetNoAccountForNoMatchingIdsOrNull(double id) {
            Account account = new() { TeamspeakIdentities = Math.Abs(id - -1) < 0.01 ? null : new HashSet<double> { id } };
            List<Account> mockAccountCollection = new() { account };

            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns<Func<Account, bool>>(x => mockAccountCollection.FirstOrDefault(x));
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>())).Returns(Task.CompletedTask);

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(null, new List<double> { 5 }, 1), Times.Once);
        }

        [Fact]
        public void LogOnException() {
            _teamspeakServerEventHandler.Init();

            _eventBus.Send(new SignalrEventData { Procedure = (TeamspeakEventType) 9 });

            _mockLoggingService.Verify(x => x.LogError(It.IsAny<ArgumentException>()), Times.Once);
        }

        [Fact]
        public void ShouldCorrectlyParseClients() {
            HashSet<TeamspeakClient> subject = new();
            _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>())).Callback((HashSet<TeamspeakClient> x) => subject = x);

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(
                new SignalrEventData {
                    Procedure = TeamspeakEventType.CLIENTS,
                    Args = "[{\"channelId\": 1, \"channelName\": \"Test Channel 1\", \"clientDbId\": 5, \"clientName\": \"Test Name 1\"}," +
                           "{\"channelId\": 2, \"channelName\": \"Test Channel 2\", \"clientDbId\": 10, \"clientName\": \"Test Name 2\"}]"
                }
            );

            subject.Should().HaveCount(2);
            subject.Should()
                   .BeEquivalentTo(
                       new HashSet<TeamspeakClient> {
                           new() { ChannelId = 1, ChannelName = "Test Channel 1", ClientDbId = 5, ClientName = "Test Name 1" },
                           new() { ChannelId = 2, ChannelName = "Test Channel 2", ClientDbId = 10, ClientName = "Test Name 2" }
                       }
                   );
        }

        [Fact]
        public async Task ShouldGetCorrectAccount() {
            Account account1 = new() { TeamspeakIdentities = new HashSet<double> { 1 } };
            Account account2 = new() { TeamspeakIdentities = new HashSet<double> { 2 } };
            List<Account> mockAccountCollection = new() { account1, account2 };

            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns<Func<Account, bool>>(x => mockAccountCollection.FirstOrDefault(x));
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(account1, It.IsAny<ICollection<double>>(), 1)).Returns(Task.CompletedTask);

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account1, new List<double> { 5 }, 1), Times.Once);
        }

        [Fact]
        public void ShouldNotRunEventOnEmpty() {
            _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.EMPTY });

            _mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Never);
            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()), Times.Never);
        }

        [Fact]
        public void ShouldNotRunUpdateClientsForNoClients() {
            _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENTS, Args = "[]" });

            _mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Never);
        }

        [Fact]
        public async Task ShouldRunClientGroupsUpdate() {
            Account account = new() { TeamspeakIdentities = new HashSet<double> { 1 } };

            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns(account);
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(account, It.IsAny<ICollection<double>>(), 1)).Returns(Task.CompletedTask);

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> { 5 }, 1), Times.Once);
        }

        [Fact]
        public async Task ShouldRunClientGroupsUpdateTwiceForTwoEventsWithDelay() {
            Account account = new() { TeamspeakIdentities = new HashSet<double> { 1 } };

            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns(account);
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));
            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, Args = "{\"clientDbid\": 1, \"serverGroupId\": 10}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> { 5 }, 1), Times.Once);
            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> { 10 }, 1), Times.Once);
        }

        [Fact]
        public async Task ShouldRunSingleClientGroupsUpdateForEachClient() {
            Account account1 = new() { TeamspeakIdentities = new HashSet<double> { 1 } };
            Account account2 = new() { TeamspeakIdentities = new HashSet<double> { 2 } };
            List<Account> mockCollection = new() { account1, account2 };

            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns<Func<Account, bool>>(x => mockCollection.FirstOrDefault(x));
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, Args = "{\"clientDbid\": 2, \"serverGroupId\": 10}" });
            await Task.Delay(TimeSpan.FromSeconds(2));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account1, new List<double> { 5 }, 1), Times.Once);
            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account2, new List<double> { 10 }, 2), Times.Once);
        }

        [Fact]
        public async Task ShouldRunSingleClientGroupsUpdateForMultipleEventsWithOneClient() {
            Account account = new() { TeamspeakIdentities = new HashSet<double> { 1 } };

            _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns(account);
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, Args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            _eventBus.Send(new SignalrEventData { Procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, Args = "{\"clientDbid\": 1, \"serverGroupId\": 10}" });
            await Task.Delay(TimeSpan.FromSeconds(2));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> { 5, 10 }, 1), Times.Once);
        }

        [Fact]
        public void ShouldRunUpdateClients() {
            _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));

            _teamspeakServerEventHandler.Init();

            _eventBus.Send(
                new SignalrEventData { Procedure = TeamspeakEventType.CLIENTS, Args = "[{\"channelId\": 1, \"channelName\": \"Test Channel\", \"clientDbId\": 5, \"clientName\": \"Test Name\"}]" }
            );

            _mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Once);
        }
    }
}
