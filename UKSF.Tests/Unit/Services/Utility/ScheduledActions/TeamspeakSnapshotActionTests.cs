using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Services;
using UKSF.Api.Teamspeak.ScheduledActions;
using UKSF.Api.Teamspeak.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility.ScheduledActions
{
    public class TeamspeakSnapshotActionTests
    {
        private readonly IActionTeamspeakSnapshot _actionTeamspeakSnapshot;
        private readonly Mock<ITeamspeakService> _mockTeamspeakService;

        public TeamspeakSnapshotActionTests()
        {
            _mockTeamspeakService = new();
            Mock<IClock> mockClock = new();
            Mock<ISchedulerService> mockSchedulerService = new();
            Mock<IHostEnvironment> mockHostEnvironment = new();
            Mock<ISchedulerContext> mockSchedulerContext = new();

            _actionTeamspeakSnapshot = new ActionTeamspeakSnapshot(
                mockSchedulerContext.Object,
                _mockTeamspeakService.Object,
                mockSchedulerService.Object,
                mockHostEnvironment.Object,
                mockClock.Object
            );
        }

        [Fact]
        public void When_getting_action_name()
        {
            string subject = _actionTeamspeakSnapshot.Name;

            subject.Should().Be("ActionTeamspeakSnapshot");
        }

        [Fact]
        public async Task When_running_snapshot()
        {
            await _actionTeamspeakSnapshot.Run();

            _mockTeamspeakService.Verify(x => x.StoreTeamspeakServerSnapshot(), Times.Once);
        }
    }
}
