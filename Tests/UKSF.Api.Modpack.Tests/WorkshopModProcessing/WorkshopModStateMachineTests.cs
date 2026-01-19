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

        machine.InstallingDownloading.Should().NotBeNull();
        machine.InstallingChecking.Should().NotBeNull();
        machine.InstallingAwaitingIntervention.Should().NotBeNull();
        machine.Installing.Should().NotBeNull();
        machine.UpdatingDownloading.Should().NotBeNull();
        machine.UpdatingChecking.Should().NotBeNull();
        machine.UpdatingAwaitingIntervention.Should().NotBeNull();
        machine.Updating.Should().NotBeNull();
        machine.Uninstalling.Should().NotBeNull();
        machine.Cleanup.Should().NotBeNull();

        machine.InstallRequested.Should().NotBeNull();
        machine.UpdateRequested.Should().NotBeNull();
        machine.UninstallRequested.Should().NotBeNull();
        machine.InstallDownloadComplete.Should().NotBeNull();
        machine.UpdateDownloadComplete.Should().NotBeNull();
        machine.InstallCheckComplete.Should().NotBeNull();
        machine.UpdateCheckComplete.Should().NotBeNull();
        machine.InterventionResolved.Should().NotBeNull();
        machine.InstallComplete.Should().NotBeNull();
        machine.UpdateComplete.Should().NotBeNull();
        machine.UninstallComplete.Should().NotBeNull();
        machine.CleanupComplete.Should().NotBeNull();
        machine.OperationFaulted.Should().NotBeNull();
    }
}
