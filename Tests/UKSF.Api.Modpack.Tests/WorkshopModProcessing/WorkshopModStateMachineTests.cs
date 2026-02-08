using FluentAssertions;
using UKSF.Api.Modpack.WorkshopModProcessing;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class WorkshopModStateMachineTests
{
    [Fact]
    public void Constructor_ShouldConfigureStatesAndEvents()
    {
        var machine = new WorkshopModStateMachine();

        machine.Downloading.Should().NotBeNull();
        machine.Checking.Should().NotBeNull();
        machine.AwaitingIntervention.Should().NotBeNull();
        machine.Executing.Should().NotBeNull();
        machine.Uninstalling.Should().NotBeNull();
        machine.Cleanup.Should().NotBeNull();

        machine.InstallRequested.Should().NotBeNull();
        machine.UpdateRequested.Should().NotBeNull();
        machine.UninstallRequested.Should().NotBeNull();
        machine.DownloadComplete.Should().NotBeNull();
        machine.CheckComplete.Should().NotBeNull();
        machine.InterventionResolved.Should().NotBeNull();
        machine.ExecuteComplete.Should().NotBeNull();
        machine.UninstallComplete.Should().NotBeNull();
        machine.CleanupComplete.Should().NotBeNull();
        machine.OperationFaulted.Should().NotBeNull();
    }
}
