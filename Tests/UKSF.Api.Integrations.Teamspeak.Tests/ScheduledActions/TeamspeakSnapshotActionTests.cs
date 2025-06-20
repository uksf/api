using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.ScheduledActions;
using UKSF.Api.Integrations.Teamspeak.Services;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.ScheduledActions;

public class TeamspeakSnapshotActionTests
{
    private readonly IActionTeamspeakSnapshot _actionTeamspeakSnapshot;

    public TeamspeakSnapshotActionTests()
    {
        Mock<ITeamspeakService> mockTeamspeakService = new();
        Mock<IClock> mockClock = new();
        Mock<ISchedulerService> mockSchedulerService = new();
        Mock<IHostEnvironment> mockHostEnvironment = new();
        Mock<ISchedulerContext> mockSchedulerContext = new();

        _actionTeamspeakSnapshot = new ActionTeamspeakSnapshot(
            mockSchedulerContext.Object,
            mockTeamspeakService.Object,
            mockSchedulerService.Object,
            mockHostEnvironment.Object,
            mockClock.Object
        );
    }

    [Fact]
    public void When_getting_action_name()
    {
        var subject = _actionTeamspeakSnapshot.Name;

        subject.Should().Be("ActionTeamspeakSnapshot");
    }

    // [Fact]
    // public async Task When_running_snapshot()
    // {
    //     await _actionTeamspeakSnapshot.Run();
    //
    //     _mockTeamspeakService.Verify(x => x.StoreTeamspeakServerSnapshot(), Times.Once);
    // }
}
