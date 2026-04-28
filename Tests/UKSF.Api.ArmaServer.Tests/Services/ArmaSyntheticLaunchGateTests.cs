using FluentAssertions;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class ArmaSyntheticLaunchGateTests
{
    [Fact]
    public void First_caller_acquires_then_second_is_rejected_until_release()
    {
        var gate = new ArmaSyntheticLaunchGate();

        gate.TryAcquire("run-A").Should().BeTrue();
        gate.TryAcquire("run-B").Should().BeFalse();
        gate.CurrentRunId.Should().Be("run-A");

        gate.Release();
        gate.CurrentRunId.Should().BeNull();

        gate.TryAcquire("run-B").Should().BeTrue();
        gate.CurrentRunId.Should().Be("run-B");
    }

    [Fact]
    public void Release_when_not_held_is_a_noop()
    {
        var gate = new ArmaSyntheticLaunchGate();
        var act = () => gate.Release();
        act.Should().NotThrow();
        gate.CurrentRunId.Should().BeNull();
    }
}
