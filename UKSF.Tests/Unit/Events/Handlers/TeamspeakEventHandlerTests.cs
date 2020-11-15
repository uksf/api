using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.EventHandlers;
using UKSF.Api.Teamspeak.Models;
using UKSF.Api.Teamspeak.Services;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class TeamspeakEventHandlerTests {
        private readonly Mock<IAccountService> _mockAccountService;
        private readonly Mock<ILogger> _mockLoggingService;
        private readonly Mock<ITeamspeakGroupService> _mockTeamspeakGroupService;
        private readonly Mock<ITeamspeakService> _mockTeamspeakService;
        private readonly ISignalrEventBus _signalrEventBus;
        private readonly TeamspeakEventHandler _teamspeakEventHandler;

        public TeamspeakEventHandlerTests() {
            _signalrEventBus = new SignalrEventBus();
            _mockTeamspeakService = new Mock<ITeamspeakService>();
            _mockAccountService = new Mock<IAccountService>();
            _mockTeamspeakGroupService = new Mock<ITeamspeakGroupService>();
            _mockLoggingService = new Mock<ILogger>();

            _teamspeakEventHandler = new TeamspeakEventHandler(_signalrEventBus, _mockTeamspeakService.Object, _mockAccountService.Object, _mockTeamspeakGroupService.Object, _mockLoggingService.Object);
        }

        [Theory, InlineData(2), InlineData(-1)]
        public async Task ShouldGetNoAccountForNoMatchingIdsOrNull(double id) {
            Account account = new Account { teamspeakIdentities = Math.Abs(id - -1) < 0.01 ? null : new HashSet<double> { id } };
            List<Account> mockAccountCollection = new List<Account> { account };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns<Func<Account, bool>>(x => mockAccountCollection.FirstOrDefault(x));
            _mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>())).Returns(Task.CompletedTask);

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(null, new List<double> { 5 }, 1), Times.Once);
        }

        [Fact]
        public void LogOnException() {
            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(new SignalrEventModel { procedure = (TeamspeakEventType) 9 });

            _mockLoggingService.Verify(x => x.LogError(It.IsAny<ArgumentException>()), Times.Once);
        }

        [Fact]
        public void ShouldCorrectlyParseClients() {
            HashSet<TeamspeakClient> subject = new HashSet<TeamspeakClient>();
            _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>())).Callback((HashSet<TeamspeakClient> x) => subject = x);

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(
                new SignalrEventModel {
                    procedure = TeamspeakEventType.CLIENTS,
                    args = "[{\"channelId\": 1, \"channelName\": \"Test Channel 1\", \"clientDbId\": 5, \"clientName\": \"Test Name 1\"}," +
                           "{\"channelId\": 2, \"channelName\": \"Test Channel 2\", \"clientDbId\": 10, \"clientName\": \"Test Name 2\"}]"
                }
            );

            subject.Should().HaveCount(2);
            subject.Should()
                   .BeEquivalentTo(
                       new HashSet<TeamspeakClient> {
                           new TeamspeakClient { channelId = 1, channelName = "Test Channel 1", clientDbId = 5, clientName = "Test Name 1" },
                           new TeamspeakClient { channelId = 2, channelName = "Test Channel 2", clientDbId = 10, clientName = "Test Name 2" }
                       }
                   );
        }

        [Fact]
        public async Task ShouldGetCorrectAccount() {
            Account account1 = new Account { teamspeakIdentities = new HashSet<double> { 1 } };
            Account account2 = new Account { teamspeakIdentities = new HashSet<double> { 2 } };
            List<Account> mockAccountCollection = new List<Account> { account1, account2 };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns<Func<Account, bool>>(x => mockAccountCollection.FirstOrDefault(x));
            _mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(account1, It.IsAny<ICollection<double>>(), 1)).Returns(Task.CompletedTask);

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account1, new List<double> { 5 }, 1), Times.Once);
        }

        [Fact]
        public void ShouldNotRunEventOnEmpty() {
            _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.EMPTY });

            _mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Never);
            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()), Times.Never);
        }

        [Fact]
        public void ShouldNotRunUpdateClientsForNoClients() {
            _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENTS, args = "[]" });

            _mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Never);
        }

        [Fact]
        public async Task ShouldRunClientGroupsUpdate() {
            Account account = new Account { teamspeakIdentities = new HashSet<double> { 1 } };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns(account);
            _mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(account, It.IsAny<ICollection<double>>(), 1)).Returns(Task.CompletedTask);

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> { 5 }, 1), Times.Once);
        }

        [Fact]
        public async Task ShouldRunClientGroupsUpdateTwiceForTwoEventsWithDelay() {
            Account account = new Account { teamspeakIdentities = new HashSet<double> { 1 } };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns(account);
            _mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));
            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 10}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> { 5 }, 1), Times.Once);
            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> { 10 }, 1), Times.Once);
        }

        [Fact]
        public async Task ShouldRunSingleClientGroupsUpdateForEachClient() {
            Account account1 = new Account { teamspeakIdentities = new HashSet<double> { 1 } };
            Account account2 = new Account { teamspeakIdentities = new HashSet<double> { 2 } };
            List<Account> mockCollection = new List<Account> { account1, account2 };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns<Func<Account, bool>>(x => mockCollection.FirstOrDefault(x));
            _mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 2, \"serverGroupId\": 10}" });
            await Task.Delay(TimeSpan.FromSeconds(2));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account1, new List<double> { 5 }, 1), Times.Once);
            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account2, new List<double> { 10 }, 2), Times.Once);
        }

        [Fact]
        public async Task ShouldRunSingleClientGroupsUpdateForMultipleEventsWithOneClient() {
            Account account = new Account { teamspeakIdentities = new HashSet<double> { 1 } };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns(account);
            _mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            _mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            _signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 10}" });
            await Task.Delay(TimeSpan.FromSeconds(2));

            _mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> { 5, 10 }, 1), Times.Once);
        }

        [Fact]
        public void ShouldRunUpdateClients() {
            _mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));

            _teamspeakEventHandler.Init();

            _signalrEventBus.Send(
                new SignalrEventModel { procedure = TeamspeakEventType.CLIENTS, args = "[{\"channelId\": 1, \"channelName\": \"Test Channel\", \"clientDbId\": 5, \"clientName\": \"Test Name\"}]" }
            );

            _mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Once);
        }
    }
}
