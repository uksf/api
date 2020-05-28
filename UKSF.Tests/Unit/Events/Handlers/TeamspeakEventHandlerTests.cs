using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Events.SignalrServer;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Events.Types;
using UKSF.Api.Models.Integrations;
using UKSF.Api.Models.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Events.Handlers {
    public class TeamspeakEventHandlerTests {
        private readonly Mock<IAccountService> mockAccountService;
        private readonly Mock<ILoggingService> mockLoggingService;
        private readonly Mock<ITeamspeakGroupService> mockTeamspeakGroupService;
        private readonly Mock<ITeamspeakService> mockTeamspeakService;
        private readonly ISignalrEventBus signalrEventBus;
        private readonly TeamspeakEventHandler teamspeakEventHandler;

        public TeamspeakEventHandlerTests() {
            signalrEventBus = new SignalrEventBus();
            mockTeamspeakService = new Mock<ITeamspeakService>();
            mockAccountService = new Mock<IAccountService>();
            mockTeamspeakGroupService = new Mock<ITeamspeakGroupService>();
            mockLoggingService = new Mock<ILoggingService>();

            teamspeakEventHandler = new TeamspeakEventHandler(signalrEventBus, mockTeamspeakService.Object, mockAccountService.Object, mockTeamspeakGroupService.Object, mockLoggingService.Object);
        }

        [Fact]
        public void ShouldCorrectlyParseClients() {
            HashSet<TeamspeakClient> subject = new HashSet<TeamspeakClient>();
            mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>())).Callback((HashSet<TeamspeakClient> x) => subject = x);

            teamspeakEventHandler.Init();

            signalrEventBus.Send(
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
        public void ShouldNotRunEventOnEmpty() {
            mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));
            mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            teamspeakEventHandler.Init();

            signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.EMPTY });

            mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Never);
            mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()), Times.Never);
        }

        [Fact]
        public void ShouldNotRunUpdateClientsForNoClients() {
            mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));

            teamspeakEventHandler.Init();

            signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENTS, args = "[]" });

            mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Never);
        }

        [Fact]
        public async Task ShouldRunClientGroupsUpdate() {
            Account account = new Account { teamspeakIdentities = new HashSet<double> { 1 } };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns(account);
            mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(account, It.IsAny<ICollection<double>>(), 1)).Returns(Task.CompletedTask);

            teamspeakEventHandler.Init();

            signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> { 5 }, 1), Times.Once);
        }

        [Fact]
        public async Task ShouldRunSingleClientGroupsUpdateForEachClient() {
            Account account1 = new Account { teamspeakIdentities = new HashSet<double> { 1 } };
            Account account2 = new Account { teamspeakIdentities = new HashSet<double> { 2 } };
            List<Account> mockCollection = new List<Account> { account1, account2 };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns<Func<Account, bool>>(x => mockCollection.FirstOrDefault(x));
            mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            teamspeakEventHandler.Init();

            signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 2, \"serverGroupId\": 10}" });
            await Task.Delay(TimeSpan.FromSeconds(2));

            mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account1, new List<double> {5}, 1), Times.Once);
            mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account2, new List<double> {10}, 2), Times.Once);
        }

        [Fact]
        public async Task ShouldRunSingleClientGroupsUpdateForMultipleEventsWithOneClient() {
            Account account = new Account { teamspeakIdentities = new HashSet<double> { 1 } };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns(account);
            mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            teamspeakEventHandler.Init();

            signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 10}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> {5, 10}, 1), Times.Once);
        }

        [Fact]
        public async Task ShouldRunClientGroupsUpdateTwiceForTwoEventsWithDelay() {
            Account account = new Account { teamspeakIdentities = new HashSet<double> { 1 } };
            Mock<IAccountDataService> mockAccountDataService = new Mock<IAccountDataService>();

            mockAccountDataService.Setup(x => x.GetSingle(It.IsAny<Func<Account, bool>>())).Returns(account);
            mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
            mockTeamspeakGroupService.Setup(x => x.UpdateAccountGroups(It.IsAny<Account>(), It.IsAny<ICollection<double>>(), It.IsAny<double>()));

            teamspeakEventHandler.Init();

            signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 5}" });
            await Task.Delay(TimeSpan.FromSeconds(1));
            signalrEventBus.Send(new SignalrEventModel { procedure = TeamspeakEventType.CLIENT_SERVER_GROUPS, args = "{\"clientDbid\": 1, \"serverGroupId\": 10}" });
            await Task.Delay(TimeSpan.FromSeconds(1));

            mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> {5}, 1), Times.Once);
            mockTeamspeakGroupService.Verify(x => x.UpdateAccountGroups(account, new List<double> {10}, 1), Times.Once);
        }

        [Fact]
        public void ShouldRunUpdateClients() {
            mockTeamspeakService.Setup(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()));

            teamspeakEventHandler.Init();

            signalrEventBus.Send(
                new SignalrEventModel { procedure = TeamspeakEventType.CLIENTS, args = "[{\"channelId\": 1, \"channelName\": \"Test Channel\", \"clientDbId\": 5, \"clientName\": \"Test Name\"}]" }
            );

            mockTeamspeakService.Verify(x => x.UpdateClients(It.IsAny<HashSet<TeamspeakClient>>()), Times.Once);
        }

        [Fact]
        public void LogOnException() {
            mockLoggingService.Setup(x => x.Log(It.IsAny<Exception>()));

            teamspeakEventHandler.Init();

            signalrEventBus.Send(new SignalrEventModel { procedure = (TeamspeakEventType) 9 });

            mockLoggingService.Verify(x => x.Log(It.IsAny<Exception>()), Times.Once);
        }
    }
}
