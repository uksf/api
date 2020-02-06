using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Events.Types;
using UKSF.Api.Signalr.Hubs.Integrations;
using Xunit;

namespace UKSF.Tests.Unit.Signalr {
    public class TeamspeakHubTests {
        public TeamspeakHubTests() => mockSignalrEventBus = new Mock<ISignalrEventBus>();

        private readonly Mock<ISignalrEventBus> mockSignalrEventBus;

        [Fact]
        public void ShouldSendEvent() {
            SignalrEventModel expected = new SignalrEventModel {procedure = TeamspeakEventType.EMPTY, args = "test"};
            SignalrEventModel subject = null;

            mockSignalrEventBus.Setup(x => x.Send(It.IsAny<SignalrEventModel>())).Callback((SignalrEventModel x) => subject = x);

            TeamspeakHub teamspeakHub = new TeamspeakHub(mockSignalrEventBus.Object);
            teamspeakHub.Invoke(0, "test");

            subject.Should().NotBeNull();
            subject.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task ShouldSetConnected() {
            TeamspeakHubState.Connected = false;

            TeamspeakHub teamspeakHub = new TeamspeakHub(mockSignalrEventBus.Object);
            await teamspeakHub.OnConnectedAsync();

            TeamspeakHubState.Connected.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldUnsetConnected() {
            TeamspeakHubState.Connected = true;

            TeamspeakHub teamspeakHub = new TeamspeakHub(mockSignalrEventBus.Object);
            await teamspeakHub.OnDisconnectedAsync(null);

            TeamspeakHubState.Connected.Should().BeFalse();
        }
    }
}
