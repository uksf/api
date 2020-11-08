using FluentAssertions;
using Moq;
using UKSF.Api.Base.Services;
using UKSF.Api.Teamspeak.ScheduledActions;
using UKSF.Api.Teamspeak.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions {
    public class TeamspeakSnapshotActionTests {
        private readonly Mock<IClock> _mockClock;
        private readonly Mock<ISchedulerService> _mockSchedulerService;
        private readonly Mock<ITeamspeakService> mockTeamspeakService;
        private IActionTeamspeakSnapshot actionTeamspeakSnapshot;

        public TeamspeakSnapshotActionTests() {
            mockTeamspeakService = new Mock<ITeamspeakService>();
            _mockClock = new Mock<IClock>();
            _mockSchedulerService = new Mock<ISchedulerService>();
        }

        [Fact]
        public void ShouldReturnActionName() {
            actionTeamspeakSnapshot = new ActionTeamspeakSnapshot(mockTeamspeakService.Object, _mockSchedulerService.Object, _mockClock.Object);

            string subject = actionTeamspeakSnapshot.Name;

            subject.Should().Be("TeamspeakSnapshotAction");
        }

        [Fact]
        public void ShouldRunSnapshot() {
            actionTeamspeakSnapshot = new ActionTeamspeakSnapshot(mockTeamspeakService.Object, _mockSchedulerService.Object, _mockClock.Object);

            actionTeamspeakSnapshot.Run();

            mockTeamspeakService.Verify(x => x.StoreTeamspeakServerSnapshot(), Times.Once);
        }
    }
}
