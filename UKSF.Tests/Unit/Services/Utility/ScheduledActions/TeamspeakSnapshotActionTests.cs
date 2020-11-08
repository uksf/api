using FluentAssertions;
using Moq;
using UKSF.Api.Teamspeak.ScheduledActions;
using UKSF.Api.Teamspeak.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions {
    public class TeamspeakSnapshotActionTests {
        private readonly Mock<ITeamspeakService> mockTeamspeakService;
        private ITeamspeakSnapshotAction teamspeakSnapshotAction;

        public TeamspeakSnapshotActionTests() => mockTeamspeakService = new Mock<ITeamspeakService>();

        [Fact]
        public void ShouldReturnActionName() {
            teamspeakSnapshotAction = new TeamspeakSnapshotAction(mockTeamspeakService.Object);

            string subject = teamspeakSnapshotAction.Name;

            subject.Should().Be("TeamspeakSnapshotAction");
        }

        [Fact]
        public void ShouldRunSnapshot() {
            teamspeakSnapshotAction = new TeamspeakSnapshotAction(mockTeamspeakService.Object);

            teamspeakSnapshotAction.Run();

            mockTeamspeakService.Verify(x => x.StoreTeamspeakServerSnapshot(), Times.Once);
        }
    }
}
